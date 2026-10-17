using System.Drawing.Drawing2D;

namespace SensSync.Desktop;

internal static class UiTheme
{
    public static Color Background => SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(245, 247, 250);
    public static Color Ink => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(28, 39, 56);
    public static Color Muted => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(79, 94, 113);
    public static Color Tint => SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(232, 240, 252);
    public static TableLayoutPanel Stack() => new() { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Margin = Padding.Empty };
    public static TableLayoutPanel Card(string name) => new CardPanel
    { Name = name, AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Padding = new(16, 12, 16, 12), BackColor = SystemColors.Window };
    public static Label Caption(string text) => new() { Text = text, Tag = "Muted", AutoSize = true, ForeColor = Muted, Margin = new(0, 3, 0, 6) };

    public static void StyleButtons(Control root, Button? primary = null)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = button == primary ? 0 : 1;
                button.FlatAppearance.BorderColor = SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(202, 212, 225);
                button.Padding = new Padding(10, 5, 10, 5);
                button.Cursor = Cursors.Hand;
                button.BackColor = button == primary ? (SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(37, 88, 180)) : SystemColors.Window;
                button.ForeColor = button == primary ? SystemColors.HighlightText : Ink;
                button.UseVisualStyleBackColor = false;
            }
            StyleButtons(control, primary);
        }
    }

    internal static void ConfigureCombo(ComboBox combo)
    {
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.DrawItem += (_, e) =>
        {
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color background = selected ? SystemColors.Highlight : combo.BackColor;
            Color foreground = selected ? SystemColors.HighlightText : combo.ForeColor;
            using var brush = new SolidBrush(background);
            e.Graphics.FillRectangle(brush, e.Bounds);
            string text = e.Index >= 0 ? combo.Items[e.Index]?.ToString() ?? "" : combo.Text;
            TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
    }

    internal static void Apply(Control root, bool dark, Button? primary = null)
    {
        bool highContrast = SystemInformation.HighContrast;
        dark &= !highContrast;
        Color background = dark ? Color.FromArgb(20, 25, 34) : Background;
        Color surface = highContrast ? SystemColors.Window : dark ? Color.FromArgb(30, 37, 49) : Color.White;
        Color input = dark ? Color.FromArgb(39, 48, 63) : SystemColors.Window;
        Color tint = dark ? Color.FromArgb(32, 48, 72) : Tint;
        Color ink = dark ? Color.FromArgb(237, 242, 250) : Ink;
        Color muted = dark ? Color.FromArgb(180, 195, 216) : Muted;
        Color border = highContrast ? SystemColors.WindowText : dark ? Color.FromArgb(89, 107, 132) : Color.FromArgb(202, 212, 225);
        void Visit(Control control, Color parentBackground)
        {
            control.ForeColor = ink;
            control.BackColor = control switch
            {
                Form => background,
                CardPanel => control.Name == "ResultCard" ? tint : surface,
                TabPage => surface,
                TextBox => control.Name == "Result" ? tint : input,
                ComboBox => input,
                _ => control.Name == "ReadOnlyBadge" ? tint : parentBackground
            };
            if (control is Label && (control.Tag as string == "Muted" || control.Name is "Detection" or "Physical" or "Status" or "Note"))
                control.ForeColor = muted;
            if (control is Button button)
            {
                button.FlatAppearance.BorderColor = border;
                button.BackColor = button == primary
                    ? button.Enabled ? (highContrast ? SystemColors.Highlight : Color.FromArgb(37, 88, 180))
                        : highContrast ? SystemColors.Control : dark ? Color.FromArgb(58, 70, 88) : Color.FromArgb(213, 223, 238)
                    : surface;
                button.ForeColor = button == primary && button.Enabled ? SystemColors.HighlightText : ink;
            }
            if (control is ComboBox combo) combo.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
            foreach (Control child in control.Controls) Visit(child, control.BackColor);
            control.Invalidate();
        }
        root.SuspendLayout();
        Visit(root, background);
        root.ResumeLayout();
        if (root is Form form && form.IsHandleCreated)
        {
            int enabled = dark ? 1 : 0;
            // Cosmetic only. Unsupported Windows versions simply return an error code.
            DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

    private sealed class CardPanel : TableLayoutPanel
    {
        public CardPanel() => DoubleBuffered = true;
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Background);
            float radius = 12 * DeviceDpi / 96f;
            var rect = new RectangleF(0, 0, Width - 1, Height - 1);
            using var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, radius, radius, 180, 90);
            path.AddArc(rect.Right - radius, rect.Top, radius, radius, 270, 90);
            path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);
            if (SystemInformation.HighContrast)
            {
                using var border = new Pen(SystemColors.WindowText);
                e.Graphics.DrawPath(border, path);
            }
        }
    }
}
