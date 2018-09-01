using FastColoredTextBoxNS;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;

namespace gekko
{
    public class SubstringAutocompleteItem : AutocompleteItem
    {
        protected readonly string lowercaseText;
        protected readonly bool ignoreCase;

        public SubstringAutocompleteItem(string text, bool ignoreCase = true)
            : base(text)
        {
            this.ignoreCase = ignoreCase;
            if (ignoreCase)
                lowercaseText = text.ToLower();
        }

        public override CompareResult Compare(string fragmentText)
        {
            if (ignoreCase)
            {
                if (lowercaseText.Contains(fragmentText.ToLower()))
                    return CompareResult.Visible;
            }
            else
            {
                if (Text.Contains(fragmentText))
                    return CompareResult.Visible;
            }

            return CompareResult.Hidden;
        }
    }

    public class ITS_EDITOR : FastColoredTextBox
    {

        TextStyle KeywordStyle = new TextStyle(Brushes.Blue, null, FontStyle.Regular);
        TextStyle HexStyle = new TextStyle(Brushes.DarkCyan, null, FontStyle.Regular);
        TextStyle DecStyle = new TextStyle(Brushes.Red, null, FontStyle.Regular);
        TextStyle StrStyle = new TextStyle(Brushes.Chocolate, null, FontStyle.Regular);
        TextStyle CommentStyle = new TextStyle(Brushes.DarkGreen, null, FontStyle.Regular);

        public AutocompleteMenu AutocompleteMenu { get; set; }

        public bool SyntaxHighlighting
        {
            get { return _syntaxHighlight; }
            set { _syntaxHighlight = value; UpdateTextStyles(); }
        }
        private bool _syntaxHighlight = true;

        public void SetAutocomplete(string[] autocomplete)
        {
            if (autocomplete != null)
            {
                var menu = new AutocompleteMenu(this) { AppearInterval = 1 };
                menu.Items.MaximumSize = new Size(int.MaxValue, menu.Items.MaximumSize.Height);
                menu.Items.Size = this.Size;
                menu.ShowItemToolTips = true;
                menu.ToolTipDuration = int.MaxValue;
                menu.Items.SetAutocompleteItems(autocomplete);
                this.AutocompleteMenu = menu;
                this.AutoCompleteBrackets = true;
                this.AutoIndent = true;
            }
        }
        public void SetAutocomplete(AutocompleteItem[] autocomplete)
        {
            if (autocomplete != null)
            {
                var menu = new AutocompleteMenu(this) { AppearInterval = 1 };
                menu.Items.MaximumSize = new Size(int.MaxValue, menu.Items.MaximumSize.Height);
                menu.Items.Size = this.Size;
                menu.ShowItemToolTips = true;
                menu.ToolTipDuration = int.MaxValue;
                menu.Items.SetAutocompleteItems(autocomplete);

                this.AutocompleteMenu = menu;
                this.AutoCompleteBrackets = true;
                this.AutoIndent = true;
            }
        }

        public ITS_EDITOR()
        {
            this.TextChanged += NewBox_TextChanged;
            this.AutoCompleteBrackets = true;
            this.AutoIndent = true;
            this.ChangedLineColor = Color.Yellow;
        }
        public void UpdateTextStyles()
        {
            UpdateTextStyles(this.Range);
        }
        public void UpdateTextStyles(Range changedRange)
        {
            if (SyntaxHighlighting)
            {
                //clear previous highlighting
                changedRange.ClearStyle(StyleIndex.All);

                //highlight tags
                changedRange.SetStyle(CommentStyle, @"(#.+)");
                //changedRange.SetStyle(KeywordStyle, @"\b(if|else|try|catch|MoveDef|Unlisted)\b");
                //changedRange.SetStyle(KeywordStyle, @"([a-zA-Z])+(?==)");
                //changedRange.SetStyle(HexStyle, @"0x[^\)\s,\r\n]+");
                //changedRange.SetStyle(DecStyle, @"\b(?:[0-9]*\\.)?[0-9]+\b");
                changedRange.SetStyle(StrStyle, "\"(\\.|[^\"])*\"");
            }
            else
            {
                //clear previous highlighting
                changedRange.ClearStyle(StyleIndex.All);
            }
        }

        private void NewBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTextStyles(e.ChangedRange);
        }
    }
}