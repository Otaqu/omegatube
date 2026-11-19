using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using static OmegaTube.I18n;

namespace OmegaTube.Ui
{
    // A text field with an on-screen keyboard (TV remotes have none): used for the API key and model rows.
    //  - arrows move, OK types the character, Back cancels; Done hands the value back.
    public class KeyEntryScreen : Screen
    {
        private const int KeyW = 108, KeyH = 74, Gap = 10, X0 = 240, Y0 = 430;
        private static readonly string[] LetterRows = { "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm" };
        private static readonly string[] ActionRow = { "abc", "-", "_", ".", "⌫", "Done", "Cancel" };

        private class Cell
        {
            public Widgets.PillButton Button;
            public string Text;
        }

        private readonly Action<string> onDone;
        private readonly TextLabel valueLabel;
        private readonly List<List<Cell>> rows = new List<List<Cell>>();
        private string value;
        private bool upper;
        private int row, col;

        public KeyEntryScreen(App app, string title, string initial, Action<string> onDone) : base(app)
        {
            this.onDone = onDone;
            value = initial ?? "";
            Root.BackgroundColor = Theme.Background;

            var t = Widgets.Label(title, Theme.TitleSize, Theme.Text, 1400, 70);
            t.Position = new Position(X0, 130);
            Root.Add(t);

            var box = Widgets.Panel(Theme.Panel, 1400, 92, 16);
            box.Position = new Position(X0, 232);
            Root.Add(box);
            valueLabel = Widgets.Label("", Theme.BodySize + 2, Theme.Text, 1360, 80);
            valueLabel.Position = new Position(20, 6);
            valueLabel.VerticalAlignment = VerticalAlignment.Center;
            box.Add(valueLabel);

            var hint = Widgets.Label(T("▲▼◀▶ move    OK type    Back cancel"), Theme.SmallSize + 1, Theme.TextDim, 1400, 40);
            hint.Position = new Position(X0, 344);
            Root.Add(hint);

            for (int r = 0; r < LetterRows.Length; r++)
            {
                var cells = new List<Cell>();
                for (int c = 0; c < LetterRows[r].Length; c++)
                {
                    var b = new Widgets.PillButton(LetterRows[r][c].ToString(), null, KeyW, KeyH);
                    b.Position = new Position(X0 + c * (KeyW + Gap), Y0 + r * (KeyH + Gap));
                    Root.Add(b);
                    cells.Add(new Cell { Button = b, Text = LetterRows[r][c].ToString() });
                }
                rows.Add(cells);
            }
            {
                var cells = new List<Cell>();
                int x = X0, y = Y0 + LetterRows.Length * (KeyH + Gap);
                int[] widths = { 150, KeyW, KeyW, KeyW, KeyW, 200, 200 };
                for (int i = 0; i < ActionRow.Length; i++)
                {
                    var b = new Widgets.PillButton(ActionRow[i], null, widths[i], KeyH);
                    b.Position = new Position(x, y);
                    Root.Add(b);
                    cells.Add(new Cell { Button = b, Text = ActionRow[i] });
                    x += widths[i] + Gap;
                }
                rows.Add(cells);
            }

            UpdateValue();
            UpdateFocus();
        }

        private void UpdateValue()
        {
            string tail = value.Length > 70 ? "…" + value.Substring(value.Length - 70) : value;
            valueLabel.Text = tail + "▎";
        }

        private void UpdateFocus()
        {
            for (int r = 0; r < rows.Count; r++)
            {
                for (int c = 0; c < rows[r].Count; c++)
                {
                    rows[r][c].Button.SetFocused(r == row && c == col);
                }
            }
        }

        public override bool OnKey(string key)
        {
            switch (key)
            {
                case "Left":
                    col = Math.Max(0, col - 1);
                    break;
                case "Right":
                    col = Math.Min(rows[row].Count - 1, col + 1);
                    break;
                case "Up":
                    row = Math.Max(0, row - 1);
                    col = Math.Min(col, rows[row].Count - 1);
                    break;
                case "Down":
                    row = Math.Min(rows.Count - 1, row + 1);
                    col = Math.Min(col, rows[row].Count - 1);
                    break;
                case "Return":
                    Activate();
                    return true;
                case "XF86Back":
                case "Escape":
                    App.Pop();
                    return true;
                default:
                    return false;
            }
            UpdateFocus();
            return true;
        }

        private void Activate()
        {
            string text = rows[row][col].Text;
            if (row < LetterRows.Length)
            {
                value += upper ? text.ToUpperInvariant() : text;
                UpdateValue();
                return;
            }
            switch (text)
            {
                case "abc":
                    upper = !upper;
                    rows[row][0].Button.SetText(upper ? "ABC" : "abc");
                    break;
                case "-":
                case "_":
                case ".":
                    value += text;
                    UpdateValue();
                    break;
                case "⌫":
                    if (value.Length > 0)
                    {
                        value = value.Substring(0, value.Length - 1);
                        UpdateValue();
                    }
                    break;
                case "Done":
                    onDone?.Invoke(value.Trim());
                    App.Pop();
                    break;
                case "Cancel":
                    App.Pop();
                    break;
            }
        }
    }
}
