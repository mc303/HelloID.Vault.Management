using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using HelloID.Vault.Core.Models.Filters;

namespace HelloID.Vault.Management.Views.ReferenceData;

public partial class AdvancedFieldSearchWindow : Window
{
    private readonly ObservableCollection<FilterRow> _filterRows = new();
    private readonly List<(string FieldName, string DisplayName)> _availableFields;

    public List<FieldFilterCriteria> ResultFilters { get; private set; } = new();

    public AdvancedFieldSearchWindow(List<(string FieldName, string DisplayName)> availableFields, List<FieldFilterCriteria>? existingFilters = null)
    {
        InitializeComponent();
        _availableFields = availableFields ?? new List<(string, string)>();

        // Restore existing filters or add one empty row
        if (existingFilters != null && existingFilters.Count > 0)
        {
            foreach (var filter in existingFilters)
            {
                AddFilterRow(filter);
            }
        }
        else
        {
            AddFilterRow();
        }
    }

    private void AddFilter_Click(object sender, RoutedEventArgs e)
    {
        AddFilterRow();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _filterRows.Clear();
        FilterRowsPanel.Children.Clear();
        AddFilterRow();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        ResultFilters = _filterRows
            .Where(r => !string.IsNullOrWhiteSpace(r.FieldName) &&
                        (r.Operator is FieldFilterOperators.IsEmpty or FieldFilterOperators.IsNotEmpty ||
                         !string.IsNullOrWhiteSpace(r.Value)))
            .Select(r => new FieldFilterCriteria
            {
                FieldName = r.FieldName,
                FieldDisplayName = r.FieldDisplayName,
                Operator = r.Operator,
                Value = r.Value
            })
            .ToList();

        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AddFilterRow(FieldFilterCriteria? existing = null)
    {
        var row = new FilterRow(_availableFields);

        if (existing != null)
        {
            row.FieldName = existing.FieldName;
            row.FieldDisplayName = existing.FieldDisplayName;
            row.Operator = existing.Operator;
            row.Value = existing.Value;
        }
        else if (_availableFields.Count > 0)
        {
            row.FieldName = _availableFields[0].FieldName;
            row.FieldDisplayName = _availableFields[0].DisplayName;
        }

        _filterRows.Add(row);

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

        var fieldCombo = new ComboBox
        {
            Width = 200,
            ItemsSource = _availableFields.Select(f => f.DisplayName).ToList(),
            SelectedItem = row.FieldDisplayName,
            Margin = new Thickness(0, 0, 8, 0)
        };
        fieldCombo.SelectionChanged += (s, e) =>
        {
            if (fieldCombo.SelectedIndex >= 0)
            {
                row.FieldDisplayName = (string)fieldCombo.SelectedItem;
                row.FieldName = _availableFields[fieldCombo.SelectedIndex].FieldName;
            }
        };

        var operatorCombo = new ComboBox
        {
            Width = 130,
            ItemsSource = FieldFilterOperators.All,
            SelectedItem = row.Operator,
            Margin = new Thickness(0, 0, 8, 0)
        };
        operatorCombo.SelectionChanged += (s, e) =>
        {
            row.Operator = (string)operatorCombo.SelectedItem;
            var isEmptyOp = row.Operator is FieldFilterOperators.IsEmpty or FieldFilterOperators.IsNotEmpty;
            var valueBox = panel.Children.OfType<TextBox>().FirstOrDefault();
            if (valueBox != null)
            {
                valueBox.IsEnabled = !isEmptyOp;
                if (isEmptyOp) valueBox.Text = string.Empty;
            }
        };

        var valueBox = new TextBox
        {
            Width = 200,
            Text = row.Value ?? string.Empty,
            IsEnabled = row.Operator is not (FieldFilterOperators.IsEmpty or FieldFilterOperators.IsNotEmpty)
        };
        valueBox.TextChanged += (s, e) => row.Value = valueBox.Text;

        var removeBtn = new Button
        {
            Content = "X",
            Width = 32,
            Height = 32,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(0),
            FontSize = 11
        };
        removeBtn.Click += (s, e) =>
        {
            _filterRows.Remove(row);
            FilterRowsPanel.Children.Remove(panel);
            if (FilterRowsPanel.Children.Count == 0) AddFilterRow();
        };

        panel.Children.Add(fieldCombo);
        panel.Children.Add(operatorCombo);
        panel.Children.Add(valueBox);
        panel.Children.Add(removeBtn);

        FilterRowsPanel.Children.Add(panel);
    }

    private class FilterRow
    {
        public string FieldName { get; set; } = string.Empty;
        public string FieldDisplayName { get; set; } = string.Empty;
        public string Operator { get; set; } = FieldFilterOperators.Contains;
        public string? Value { get; set; }

        public FilterRow(List<(string FieldName, string DisplayName)> availableFields)
        {
            if (availableFields.Count > 0)
            {
                FieldName = availableFields[0].FieldName;
                FieldDisplayName = availableFields[0].DisplayName;
            }
        }
    }
}
