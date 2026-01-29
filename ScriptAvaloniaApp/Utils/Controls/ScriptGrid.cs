using Avalonia.Controls;
using ScriptLang;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Utils.Controls
{
    public class ScriptGrid : ScriptControlBase
    {

        public ScriptGrid(ObjectValue node, ScriptEngine interpreter)
            : base(node, interpreter) { }

        public override async Task<Control> CreateAsync()
        {
            var grid = new Grid();
            await ApplyAllPropertiesAsync(grid);

            // Rows
            if (Node.TryGetValue("Rows", out var rowsVal))
                grid.RowDefinitions = ParseRowDefinitions(rowsVal.AsString());

            // Columns
            if (Node.TryGetValue("Columns", out var colsVal))
                grid.ColumnDefinitions = ParseColumnDefinitions(colsVal.AsString()) ;

            // Children
            foreach (var (key, value) in Node.Properties)
            {
                if (value.Value is not ObjectValue child || !ScriptControlFactory.IsControlType(key))
                    continue;

                var ctrl = await ScriptControlFactory
                    .CreateAsync(key, child, Engine);

                if (child.TryGetValue("Row", out var r))
                    Grid.SetRow(ctrl, (int)r.AsNumber());

                if (child.TryGetValue("Column", out var c))
                    Grid.SetColumn(ctrl, (int)c.AsNumber());

                if (child.TryGetValue("RowSpan", out var rs))
                    Grid.SetRowSpan(ctrl, (int)rs.AsNumber());

                if (child.TryGetValue("ColumnSpan", out var cs))
                    Grid.SetColumnSpan(ctrl, (int)cs.AsNumber());

                grid.Children.Add(ctrl);

            }

            return grid;
        }

        private RowDefinitions ParseRowDefinitions(string s)
        {
            var defs = new RowDefinitions();
            foreach (var part in s.Split(','))
            {
                var gridLength = ParseGridLength(part.Trim());
                var rd = new RowDefinition(gridLength);
                defs.Add(rd);
            }
            return defs;
        }

        private ColumnDefinitions ParseColumnDefinitions(string s)
        {
            var defs = new ColumnDefinitions();
            foreach (var part in s.Split(','))
            {
                var gridLength = ParseGridLength(part.Trim());
                var rd = new ColumnDefinition(gridLength);
                defs.Add(rd);
            }
            return defs;
        }

        private GridLength ParseGridLength(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return GridLength.Auto;
            }
            if (string.Equals(s,"Auto", StringComparison.OrdinalIgnoreCase)) return GridLength.Auto;
            if (s.EndsWith('*'))
            {
                var v = s == "*" ? 1 : double.Parse(s.TrimEnd('*'));
                return new GridLength(v, GridUnitType.Star);
            }
            return new GridLength(double.Parse(s));
        }
    }

}
