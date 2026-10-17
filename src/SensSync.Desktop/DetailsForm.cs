namespace SensSync.Desktop;

internal sealed class DetailsForm : Form
{
    public DetailsForm(string source, string values, string notes, string diagnostics, bool dark = false)
    {
        Name = "SensitivityDetails";
        Text = "Sensitivity details · SensSync";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(560, 450);
        MinimumSize = new Size(440, 360);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Ink;
        Padding = new Padding(18);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        var tabs = new TabControl { Name = "DetailSections", Dock = DockStyle.Fill, AccessibleName = "Detail sections" };
        foreach (var (title, text) in new[] { ("Source", source), ("Detected values", values), ("Notes", notes), ("Diagnostics", diagnostics) })
        {
            var page = new TabPage(title) { Padding = new Padding(12), BackColor = SystemColors.Window };
            page.Controls.Add(new TextBox { Name = title, AccessibleName = title, Text = text, Multiline = true,
                ReadOnly = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
                BackColor = SystemColors.Window, ForeColor = UiTheme.Ink });
            tabs.TabPages.Add(page);
        }
        var close = new Button { Text = "&Close", AutoSize = true, Anchor = AnchorStyles.Right, DialogResult = DialogResult.Cancel, Margin = new(0, 12, 0, 0) };
        layout.Controls.Add(tabs, 0, 0);
        layout.Controls.Add(close, 0, 1);
        Controls.Add(layout);
        CancelButton = close;
        Shown += (_, _) => close.Focus();
        UiTheme.StyleButtons(this);
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.DrawItem += (_, e) =>
        {
            var page = tabs.TabPages[e.Index];
            using var brush = new SolidBrush(page.BackColor);
            e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, page.Text, tabs.Font, e.Bounds, page.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (tabs.Focused && e.Index == tabs.SelectedIndex)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -3, -3), page.ForeColor, page.BackColor);
        };
        HandleCreated += (_, _) => UiTheme.Apply(this, dark);
        UiTheme.Apply(this, dark);
    }
}
