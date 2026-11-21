using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Core.Models;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Data;
using HelloID.Vault.Services.Database;
using HelloID.Vault.Services.Import.Collectors;
using HelloID.Vault.Services.Import.Detection;
using HelloID.Vault.Services.Import.Mappers;
using HelloID.Vault.Services.Import.Models;
using HelloID.Vault.Services.Import.Strategies;
using HelloID.Vault.Services.Import.Utilities;
using HelloID.Vault.Services.Import.Validators;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Npgsql;
using System.Data;
using System.Diagnostics;
using System.Text.Json;

namespace HelloID.Vault.Services;

/// <summary>
/// Turso/platform, custom field and company-import parts of VaultImportService.
/// </summary>
public partial class VaultImportService
{

    /// <summary>
    /// Imports data to Turso by creating a temp SQLite database, importing, and uploading.
    /// </summary>
    private async Task<ImportResult> ImportToTursoAsync(string filePath, PrimaryManagerLogic primaryManagerLogic, bool createBackup = false, IProgress<ImportProgress>? progress = null)
    {
        if (_tursoClient == null)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = "Turso client is not configured. Please check your Turso settings."
            };
        }

        if (string.IsNullOrEmpty(_sqliteSchemaPath) || !File.Exists(_sqliteSchemaPath))
        {
            Debug.WriteLine($"[TursoImport] ERROR: SQLite schema file not found at: {_sqliteSchemaPath}");
            return new ImportResult
            {
                Success = false,
                ErrorMessage = "SQLite schema file not found. Cannot create temp database."
            };
        }

        Debug.WriteLine($"[TursoImport] Starting import to Turso");
        Debug.WriteLine($"[TursoImport] Schema path: {_sqliteSchemaPath}");
        Debug.WriteLine($"[TursoImport] Input file: {filePath}");

        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult { Success = true };
        string? tempDbPath = null;

        try
        {
            // Create temp database path
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HelloID.Vault.Management", "db");
            Directory.CreateDirectory(appDataPath);
            tempDbPath = Path.Combine(appDataPath, "tempvault.db");

            Debug.WriteLine($"[TursoImport] Temp database path: {tempDbPath}");

            // Delete existing temp file if present
            if (File.Exists(tempDbPath))
            {
                Debug.WriteLine($"[TursoImport] Deleting existing temp file");
                File.Delete(tempDbPath);
            }

            progress?.Report(new ImportProgress { CurrentOperation = "Creating temporary SQLite database..." });

            // Create temp SQLite connection factory
            var tempConnectionFactory = new SqliteConnectionFactory(tempDbPath);

            // Initialize schema
            Debug.WriteLine($"[TursoImport] Reading schema file...");
            var schemaSql = await File.ReadAllTextAsync(_sqliteSchemaPath);
            Debug.WriteLine($"[TursoImport] Schema file size: {schemaSql.Length} characters");

            using (var connection = tempConnectionFactory.CreateConnection())
            {
                Debug.WriteLine($"[TursoImport] Executing schema SQL...");
                await connection.ExecuteAsync(schemaSql);
                Debug.WriteLine($"[TursoImport] Schema created successfully");
            }

            // Clear pools
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);

            progress?.Report(new ImportProgress { CurrentOperation = "Importing data to temporary database..." });

            // Run the import using the temp connection factory
            Debug.WriteLine($"[TursoImport] Starting data import to temp SQLite...");
            result = await ImportToSqliteAsync(filePath, primaryManagerLogic, tempConnectionFactory, progress);
            Debug.WriteLine($"[TursoImport] Data import complete. Success: {result.Success}, Persons: {result.PersonsImported}, Contracts: {result.ContractsImported}");

            if (!result.Success)
            {
                Debug.WriteLine($"[TursoImport] Import failed: {result.ErrorMessage}");
                return result;
            }

            progress?.Report(new ImportProgress { CurrentOperation = "Uploading database to Turso..." });

            // Clear all pools before uploading
            SqliteConnection.ClearAllPools();
            await Task.Delay(500);

            // Get file size before upload
            var fileInfo = new FileInfo(tempDbPath);
            Debug.WriteLine($"[TursoImport] Temp database size: {fileInfo.Length / 1024} KB");

            // Upload to Turso with retries (database may need time to be ready for uploads)
            Debug.WriteLine($"[TursoImport] Starting upload to Turso...");
            Exception? lastUploadEx = null;
            bool uploaded = false;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    Debug.WriteLine($"[TursoImport] Upload attempt {attempt}/3");
                    progress?.Report(new ImportProgress { CurrentOperation = $"Uploading database to Turso... (attempt {attempt}/3)" });
                    await _tursoClient.UploadDatabaseAsync(tempDbPath);
                    uploaded = true;
                    break;
                }
                catch (Exception uploadEx)
                {
                    lastUploadEx = uploadEx;
                    Debug.WriteLine($"[TursoImport] Upload attempt {attempt} failed: {uploadEx.Message}");

                    // A 400 "database already exists" is deterministic - the upload
                    // seed is consumed and retrying cannot succeed. Break out and
                    // let the recreate flow handle it instead of wasting retries.
                    if (uploadEx.Message.Contains("database already exists", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (attempt < 3) await Task.Delay(3000);
                }
            }

            if (!uploaded && lastUploadEx != null)
            {
                throw lastUploadEx;
            }

            Debug.WriteLine($"[TursoImport] Upload successful!");
            progress?.Report(new ImportProgress { CurrentOperation = "Upload complete!" });
        }
        catch (TursoAuthException authEx)
        {
            Debug.WriteLine($"[TursoImport] Auth exception: {authEx.Message}");
            result.Success = false;
            result.ErrorMessage = $"Turso authentication failed: {(authEx.IsTokenExpired ? "Token has expired" : "Invalid token")}";
        }
        catch (TursoNetworkException netEx)
        {
            Debug.WriteLine($"[TursoImport] Network exception: {netEx.Message}");
            result.Success = false;
            result.ErrorMessage = $"Network error: {(netEx.IsOffline ? "No internet connection" : netEx.Message)}";
        }
        catch (TursoConnectionException connEx)
        {
            Debug.WriteLine($"[TursoImport] Connection exception: {connEx.Message}");

            // Check if this is an upload failure or database doesn't exist, and we have Platform API credentials for auto-create
            if ((connEx.Message.Contains("upload", StringComparison.OrdinalIgnoreCase) ||
                 connEx.Message.Contains("copying content", StringComparison.OrdinalIgnoreCase) ||
                 connEx.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                 connEx.Message.Contains("Namespace", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrEmpty(_userPreferencesService.TursoPlatformApiToken) &&
                !string.IsNullOrEmpty(_userPreferencesService.TursoDatabaseName))
            {
                Debug.WriteLine($"[TursoImport] Attempting auto-create with Platform API...");

                progress?.Report(new ImportProgress { CurrentOperation = "Upload failed. Checking if the database can be created..." });

                try
                {
                    // Try to create a new database with upload support
                    var autoCreateResult = await TryAutoCreateTursoDatabaseAsync(tempDbPath!, connEx.Message, createBackup, progress);

                    if (autoCreateResult.Success)
                    {
                        result.Success = true;
                        result.PersonsImported = autoCreateResult.PersonsImported;
                        result.ContractsImported = autoCreateResult.ContractsImported;
                        Debug.WriteLine($"[TursoImport] Auto-create successful!");
                        return result;
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = autoCreateResult.ErrorMessage ?? "Auto-create failed";
                    }
                }
                catch (Exception autoEx)
                {
                    Debug.WriteLine($"[TursoImport] Auto-create failed: {autoEx.Message}");
                    result.Success = false;
                    result.ErrorMessage = $"Auto-create failed: {autoEx.Message}\n\n" +
                        $"Original upload error:\n{connEx.Message}\n\n" +
                        $"Temp file preserved at:\n{tempDbPath}\n\n" +
                        "You can manually import using:\nturso db shell {database-name} < {tempDbPath}";
                }
            }
            else
            {
                result.Success = false;

                // Check for database not existing or upload failures
                bool isDatabaseNotFoundError = connEx.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                                               connEx.Message.Contains("Namespace", StringComparison.OrdinalIgnoreCase);
                bool isUploadError = connEx.Message.Contains("upload", StringComparison.OrdinalIgnoreCase) ||
                                     connEx.Message.Contains("copying content", StringComparison.OrdinalIgnoreCase);

                if (isDatabaseNotFoundError || isUploadError)
                {
                    if (string.IsNullOrEmpty(_userPreferencesService.TursoPlatformApiToken))
                    {
                        result.ErrorMessage = (isDatabaseNotFoundError ? "Database doesn't exist" : "Upload failed") + ".\n\n" +
                            "To enable auto-create, configure Platform API Token and Database Name in Settings.\n\n" +
                            $"Temp file preserved at:\n{tempDbPath}";
                    }
                    else
                    {
                        result.ErrorMessage = "Automatic upload failed. Use CLI workflow instead:\n\n" +
                            $"1. Keep temp file at:\n   {tempDbPath}\n\n" +
                            "2. Run: turso db import {tempDbPath}\n\n" +
                            "3. Or use Turso shell: turso db shell {database-name}";
                    }
                }
                else
                {
                    result.ErrorMessage = $"Turso connection error: {connEx.Message}";
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TursoImport] Exception: {ex.GetType().Name} - {ex.Message}");
            Debug.WriteLine($"[TursoImport] Stack trace: {ex.StackTrace}");
            result.Success = false;
            result.ErrorMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            // Clean up temp file only on success
            if (result.Success && tempDbPath != null)
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    await Task.Delay(200);

                    if (File.Exists(tempDbPath))
                    {
                        File.Delete(tempDbPath);
                        Debug.WriteLine($"[VaultImportService] Cleaned up temp file: {tempDbPath}");
                    }
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"[VaultImportService] Failed to clean up temp file: {cleanupEx.Message}");
                }
            }
            else if (!result.Success && tempDbPath != null && File.Exists(tempDbPath))
            {
                Debug.WriteLine($"[VaultImportService] Temp file preserved for manual CLI import: {tempDbPath}");
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }


    /// <summary>
    /// Attempts to create a new Turso database with upload support and import the data.
    /// Called when upload fails because the existing database doesn't support uploads.
    /// </summary>
    private async Task<ImportResult> TryAutoCreateTursoDatabaseAsync(string tempDbPath, string uploadError, bool createBackup = true, IProgress<ImportProgress>? progress = null)
    {
        var result = new ImportResult { Success = false };

        try
        {
            var platformService = new TursoPlatformService();
            platformService.SetAuthToken(_userPreferencesService.TursoPlatformApiToken!);

            var databaseName = _userPreferencesService.TursoDatabaseName!;
            var organization = _userPreferencesService.TursoOrganizationSlug;

            // /v1/upload is one-shot per database: it only works while the
            // database is still an empty "database_upload" seed placeholder.
            // Replacing an existing database therefore requires delete + recreate,
            // which is safe here because the overwrite dialog was confirmed and
            // the local temp import already succeeded.
            var isSeedConsumed = uploadError.Contains("database already exists", StringComparison.OrdinalIgnoreCase);
            var isDatabaseMissing = uploadError.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                                    uploadError.Contains("Namespace", StringComparison.OrdinalIgnoreCase);

            if (!isSeedConsumed && !isDatabaseMissing)
            {
                // Not one of the two cases auto-create can fix (e.g. a timeout).
                // Recreating on a guess would risk data loss for no reason.
                result.ErrorMessage = $"Upload failed.\n\n" +
                    $"Original upload error:\n{uploadError}\n\n" +
                    $"Temp file preserved at:\n{tempDbPath}";
                return result;
            }

            // Backup the current cloud data as a local SQLite file before any deletion,
            // only when the user chose "Backup and Overwrite" in the import dialog
            string? backupPath = null;
            if (isSeedConsumed && createBackup)
            {
                var appDataPath = Path.GetDirectoryName(tempDbPath) ?? Path.GetTempPath();
                backupPath = Path.Combine(appDataPath, $"turso_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");

                progress?.Report(new ImportProgress { CurrentOperation = "Downloading existing database backup..." });
                Debug.WriteLine($"[TursoImport] Backing up existing database to: {backupPath}");

                try
                {
                    backupPath = await _tursoClient!.DownloadDatabaseAsync(backupPath);
                }
                catch (Exception backupEx)
                {
                    // Never delete the database when the backup failed - old data stays intact
                    Debug.WriteLine($"[TursoImport] Backup download failed: {backupEx.Message}");
                    result.ErrorMessage = $"Backup of the existing database failed - import aborted, nothing was changed.\n\n" +
                        $"Original upload error:\n{uploadError}\n\n" +
                        $"Backup error:\n{backupEx.Message}";
                    return result;
                }
            }

            // Delete the existing database. The upload endpoint is one-shot per
            // database: /v1/upload only accepts a database that is still an empty
            // database_upload seed placeholder, so replacing data requires
            // delete + recreate. This runs only after the (optional) backup
            // succeeded and the local temp import is complete.
            if (isSeedConsumed)
            {
                progress?.Report(new ImportProgress { CurrentOperation = $"Deleting existing database '{databaseName}'..." });
                Debug.WriteLine($"[TursoImport] Deleting existing database: {databaseName}");

                try
                {
                    await platformService.DeleteDatabaseAsync(databaseName, organization);
                    Debug.WriteLine("[TursoImport] Database deleted successfully");

                    // Turso needs a moment to process the deletion
                    await Task.Delay(2000);
                }
                catch (Exception delEx)
                {
                    Debug.WriteLine($"[TursoImport] Delete failed: {delEx.Message}");
                    result.ErrorMessage = $"Failed to delete existing database '{databaseName}' - nothing was changed.\n\n" +
                        $"Delete error:\n{delEx.Message}\n\n" +
                        (backupPath != null ? $"Backup of the old data: {backupPath}\n" : "") +
                        $"Temp file preserved at:\n{tempDbPath}";
                    return result;
                }
            }

            progress?.Report(new ImportProgress { CurrentOperation = $"Creating Turso database '{databaseName}'..." });
            Debug.WriteLine($"[TursoImport] Creating database: {databaseName}");

            // Create the database with upload support. A documented 409 Conflict
            // means it exists again (e.g. recreated externally) - fail safely.
            TursoDatabaseResult dbResult;
            try
            {
                dbResult = await platformService.CreateDatabaseAsync(databaseName, "default", organization, forUpload: true);
            }
            catch (TursoPlatformException ex) when (ex.StatusCode == 409)
            {
                Debug.WriteLine("[TursoImport] Create returned 409 - database already exists");

                result.ErrorMessage = $"Upload failed and database '{databaseName}' already exists.\n\n" +
                    $"Original upload error:\n{uploadError}\n\n" +
                    "Delete the database manually in Turso (or via Settings) and run the import again.\n\n" +
                    (backupPath != null ? $"Backup of the old data: {backupPath}\n" : "") +
                    $"Temp file preserved at:\n{tempDbPath}";
                return result;
            }

            if (dbResult == null || string.IsNullOrEmpty(dbResult.Hostname))
            {
                result.ErrorMessage = "Failed to create database - no hostname returned";
                return result;
            }

            Debug.WriteLine($"[TursoImport] Database created: {dbResult.Url}");

            progress?.Report(new ImportProgress { CurrentOperation = "Creating database token..." });

            // Create a token for the new database
            var token = await platformService.CreateDatabaseTokenAsync(databaseName, organization);

            if (string.IsNullOrEmpty(token))
            {
                result.ErrorMessage = "Failed to create database token";
                return result;
            }

            Debug.WriteLine($"[TursoImport] Token created successfully");

            // Update preferences with new database credentials
            _userPreferencesService.TursoDatabaseUrl = dbResult.Url;
            _userPreferencesService.TursoAuthToken = token;
            await _userPreferencesService.SaveAsync();

            Debug.WriteLine($"[TursoImport] Updated preferences with new credentials");

            // Update the TursoClient with new credentials
            if (_tursoClient is TursoClient tursoClient)
            {
                tursoClient.UpdateCredentials(dbResult.Url, token);
            }

            progress?.Report(new ImportProgress { CurrentOperation = "Uploading database to new Turso database..." });

            // Retry the upload (new database may need time to be ready for uploads)
            bool uploadSuccess = false;
            Exception? lastUploadError = null;
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    Debug.WriteLine($"[TursoImport] Upload attempt {attempt}/5");
                    progress?.Report(new ImportProgress { CurrentOperation = $"Uploading database... (attempt {attempt}/5)" });
                    uploadSuccess = await _tursoClient!.UploadDatabaseAsync(tempDbPath);
                    if (uploadSuccess) break;
                }
                catch (Exception uploadEx)
                {
                    lastUploadError = uploadEx;
                    Debug.WriteLine($"[TursoImport] Upload attempt {attempt} failed: {uploadEx.Message}");
                }

                if (attempt < 5)
                {
                    await Task.Delay(3000);
                }
            }

            if (!uploadSuccess)
            {
                result.ErrorMessage = $"Failed to upload database: {lastUploadError?.Message ?? "Upload returned false"}\n\n" +
                    (backupPath != null
                        ? $"The old database was backed up before recreation: {backupPath}\n"
                        : "") +
                    $"Temp file preserved at:\n{tempDbPath}";
                return result;
            }

            Debug.WriteLine($"[TursoImport] Upload to new database successful!");

            // Get counts from temp database for result
            using (var tempConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={tempDbPath}"))
            {
                await tempConnection.OpenAsync();
                result.PersonsImported = await tempConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM persons");
                result.ContractsImported = await tempConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM contracts");
            }

            result.Success = true;
            result.NewDatabaseUrl = dbResult.Url;
            result.Message = $"Created new database: {dbResult.Url}";
        }
        catch (TursoPlatformException platEx)
        {
            Debug.WriteLine($"[TursoImport] Platform API error: {platEx.Message}");
            result.ErrorMessage = $"Platform API error: {platEx.Message}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TursoImport] Auto-create error: {ex.Message}");
            result.ErrorMessage = $"Auto-create error: {ex.Message}";
        }

        return result;
    }


    /// <summary>
    /// Imports company-only data to Turso by creating a temp SQLite database, importing, and uploading.
    /// </summary>
    private async Task<ImportResult> ImportCompanyOnlyToTursoAsync(string filePath, bool createBackup = false, IProgress<ImportProgress>? progress = null)
    {
        if (_tursoClient == null)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = "Turso client is not configured. Please check your Turso settings."
            };
        }

        if (string.IsNullOrEmpty(_sqliteSchemaPath) || !File.Exists(_sqliteSchemaPath))
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = "SQLite schema file not found. Cannot create temp database."
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult { Success = true };
        string? tempDbPath = null;

        try
        {
            // Create temp database path
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HelloID.Vault.Management", "db");
            Directory.CreateDirectory(appDataPath);
            tempDbPath = Path.Combine(appDataPath, "tempvault.db");

            // Delete existing temp file if present
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }

            progress?.Report(new ImportProgress { CurrentOperation = "Creating temporary SQLite database..." });

            // Create temp SQLite connection factory
            var tempConnectionFactory = new SqliteConnectionFactory(tempDbPath);

            // Initialize schema
            var schemaSql = await File.ReadAllTextAsync(_sqliteSchemaPath);
            using (var connection = tempConnectionFactory.CreateConnection())
            {
                await connection.ExecuteAsync(schemaSql);
            }

            // Clear pools
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);

            progress?.Report(new ImportProgress { CurrentOperation = "Importing company data to temporary database..." });

            // Run the company-only import
            result = await ImportCompanyOnlyToSqliteAsync(filePath, tempConnectionFactory, progress);

            if (!result.Success)
            {
                return result;
            }

            progress?.Report(new ImportProgress { CurrentOperation = "Uploading database to Turso..." });

            // Clear all pools before uploading
            SqliteConnection.ClearAllPools();
            await Task.Delay(500);

            // Try upload to Turso - catch exceptions to allow fallback
            bool uploadSuccess = false;
            Exception? lastUploadError = null;
            try
            {
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        Debug.WriteLine($"[TursoCompanyImport] Upload attempt {attempt}/3");
                        uploadSuccess = await _tursoClient.UploadDatabaseAsync(tempDbPath);
                        if (uploadSuccess) break;
                    }
                    catch (Exception uploadEx)
                    {
                        lastUploadError = uploadEx;
                        Debug.WriteLine($"[TursoCompanyImport] Upload attempt {attempt} failed: {uploadEx.Message}");

                        // Deterministic failure - retrying cannot succeed
                        if (uploadEx.Message.Contains("database already exists", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        if (attempt < 3) await Task.Delay(3000);
                    }
                }
            }
            catch (Exception uploadEx)
            {
                lastUploadError = uploadEx;
                Debug.WriteLine($"[TursoCompanyImport] Upload failed after retries: {uploadEx.Message}");
            }

            if (uploadSuccess)
            {
                progress?.Report(new ImportProgress { CurrentOperation = "Upload complete!" });
                return result;
            }

            // Simple upload failed - try auto-create (creates a missing database only)
            Debug.WriteLine($"[TursoCompanyImport] Simple upload failed, trying auto-create...");
            progress?.Report(new ImportProgress { CurrentOperation = "Upload failed, checking if the database can be created..." });

            if (string.IsNullOrEmpty(_userPreferencesService.TursoPlatformApiToken) ||
                string.IsNullOrEmpty(_userPreferencesService.TursoDatabaseName))
            {
                result.Success = false;
                result.ErrorMessage = "Database upload failed and Platform API is not configured.\n\n" +
                    "To enable automatic database recreation, configure Platform API Token and Database Name in Settings.";
                return result;
            }

            var autoCreateResult = await TryAutoCreateTursoDatabaseAsync(
                tempDbPath, lastUploadError?.Message ?? "Unknown upload error", createBackup, progress);

            if (autoCreateResult.Success)
            {
                result.Success = true;
                Debug.WriteLine($"[TursoCompanyImport] Auto-create successful!");
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = autoCreateResult.ErrorMessage ?? "Auto-create failed";
            }
        }
        catch (TursoAuthException authEx)
        {
            result.Success = false;
            result.ErrorMessage = $"Turso authentication failed: {(authEx.IsTokenExpired ? "Token has expired" : "Invalid token")}";
        }
        catch (TursoNetworkException netEx)
        {
            result.Success = false;
            result.ErrorMessage = $"Network error: {(netEx.IsOffline ? "No internet connection" : netEx.Message)}";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Import failed: {ex.Message}";
            Debug.WriteLine($"[VaultImportService] ImportCompanyOnlyToTursoAsync error: {ex}");
        }
        finally
        {
            // Clean up temp file
            if (tempDbPath != null)
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    await Task.Delay(200);

                    if (File.Exists(tempDbPath))
                    {
                        File.Delete(tempDbPath);
                        Debug.WriteLine($"[VaultImportService] Cleaned up temp file: {tempDbPath}");
                    }
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"[VaultImportService] Failed to clean up temp file: {cleanupEx.Message}");
                }
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }


}
