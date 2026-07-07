using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Imageboard_illust.Database;

namespace Imageboard_illust
{
    public class ColorPickerPage : Panel
    {
        public event EventHandler? BackRequested;

        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT pt);
        [DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr hdc, int x, int y);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        struct POINT { public int X, Y; }

        private Image? _loadedImage = null;
        private List<Color> _palette = new();
        private bool _eyedropping = false;
        private readonly AppDatabase _db;

        private readonly PictureBox _picBox;
        private readonly Panel _palettePanel;
        private readonly Label _hoveredColorLbl;
        private readonly Panel _hoveredSwatch;
        private readonly FlowLayoutPanel _swatchFlow;
        private readonly Label _paletteCountLbl;

        public ColorPickerPage(AppDatabase db)
        {
            _db = db;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(26, 26, 30);

            var bar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(32, 32, 38) };

            var btnBack = BarBtn("← Home", Color.FromArgb(60, 60, 70), 120);
            btnBack.Location = new Point(12, 8);
            btnBack.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);

            var barTitle = new Label
            {
                Text = "🎨  Color Picker",
                ForeColor = Color.FromArgb(210, 210, 220),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(148, 14),
            };

            var btnLoad = BarBtn("📂 Open Image", Color.FromArgb(0, 110, 190), 130);
            btnLoad.Location = new Point(340, 8);
            btnLoad.Click += (_, _) => LoadImage();

            var btnEye = BarBtn("💉 Eyedropper", Color.FromArgb(130, 60, 160), 120);
            btnEye.Location = new Point(478, 8);
            btnEye.Click += (_, _) => StartEyedropper();

            var btnClear = BarBtn("🗑 Clear Palette", Color.FromArgb(140, 35, 25), 130);
            btnClear.Location = new Point(608, 8);
            btnClear.Click += (_, _) => ClearPalette();

            _hoveredSwatch = new Panel
            {
                Size = new Size(30, 30),
                Location = new Point(800, 10),
                BackColor = Color.FromArgb(60, 60, 68),
                BorderStyle = BorderStyle.FixedSingle,
            };

            _hoveredColorLbl = new Label
            {
                Text = "#------",
                ForeColor = Color.FromArgb(180, 180, 190),
                Font = new Font("Consolas", 9f),
                AutoSize = true,
                Location = new Point(836, 18),
            };

            bar.Controls.AddRange(new Control[]
            { btnBack, barTitle, btnLoad, btnEye, btnClear, _hoveredSwatch, _hoveredColorLbl });

            var imagePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 20, 24) };

            _picBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(20, 20, 24),
                Cursor = Cursors.Cross,
            };
            _picBox.MouseMove += PicBox_MouseMove;
            _picBox.MouseClick += PicBox_Click;

            imagePanel.Controls.Add(_picBox);

            _palettePanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 260,
                BackColor = Color.FromArgb(30, 30, 36),
            };

            var paletteHeader = new Label
            {
                Text = "Color Palette",
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(26, 26, 32),
                ForeColor = Color.FromArgb(180, 180, 195),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
            };

            _paletteCountLbl = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = "0 colors",
                BackColor = Color.FromArgb(26, 26, 32),
                ForeColor = Color.FromArgb(100, 100, 115),
                Font = new Font("Segoe UI", 8f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
            };

            _swatchFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 36),
                Padding = new Padding(8),
            };

            var btnExportHex = new Button
            {
                Text = "📋 Copy HEX values",
                Dock = DockStyle.Bottom,
                Height = 38,
                BackColor = Color.FromArgb(45, 45, 55),
                ForeColor = Color.FromArgb(200, 200, 210),
                FlatStyle = FlatStyle.Flat,
            };
            btnExportHex.FlatAppearance.BorderSize = 0;
            btnExportHex.Click += ExportHex;

            var btnExportImage = new Button
            {
                Text = "🖼 Export Palette Image",
                Dock = DockStyle.Bottom,
                Height = 38,
                BackColor = Color.FromArgb(45, 45, 55),
                ForeColor = Color.FromArgb(200, 200, 210),
                FlatStyle = FlatStyle.Flat,
            };
            btnExportImage.FlatAppearance.BorderSize = 0;
            btnExportImage.Click += ExportPaletteImage;

            _palettePanel.Controls.Add(btnExportImage);
            _palettePanel.Controls.Add(btnExportHex);
            _palettePanel.Controls.Add(_swatchFlow);
            _palettePanel.Controls.Add(_paletteCountLbl);
            _palettePanel.Controls.Add(paletteHeader);

            Controls.Add(imagePanel);
            Controls.Add(_palettePanel);
            Controls.Add(bar);
        }

        private void LoadImage()
        {
            using var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
                _picBox.Image = Image.FromFile(dlg.FileName);
        }

        private void PicBox_MouseMove(object? s, MouseEventArgs e)
        {
            if (_picBox.Image == null) return;
            var bmp = new Bitmap(_picBox.Image);
            var c = bmp.GetPixel(e.X * bmp.Width / _picBox.Width,
                                 e.Y * bmp.Height / _picBox.Height);
            _hoveredSwatch.BackColor = c;
            _hoveredColorLbl.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private void PicBox_Click(object? s, MouseEventArgs e)
        {
            if (_picBox.Image == null) return;
            var bmp = new Bitmap(_picBox.Image);
            var c = bmp.GetPixel(e.X * bmp.Width / _picBox.Width,
                                 e.Y * bmp.Height / _picBox.Height);
            AddColorToPalette(c);
        }

        // ── Palette operations ────────────────────────────────────

        private void AddColorToPalette(Color c)
        {
            if (_palette.Contains(c))
            {
                Tip("Already in palette.");
                return;
            }
            _palette.Add(c);
            RebuildSwatches();
        }

        // ── Tip: briefly flashes a message in the hover label ────
        private System.Windows.Forms.Timer? _tipTimer;

        private void Tip(string msg)
        {
            _tipTimer?.Stop();
            _tipTimer?.Dispose();

            var previous = _hoveredColorLbl.Text;
            _hoveredColorLbl.ForeColor = Color.FromArgb(220, 160, 60);
            _hoveredColorLbl.Text = msg;

            _tipTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            _tipTimer.Tick += (_, _) =>
            {
                _hoveredColorLbl.Text = previous;
                _hoveredColorLbl.ForeColor = Color.FromArgb(180, 180, 190);
                _tipTimer?.Stop();
                _tipTimer?.Dispose();
                _tipTimer = null;
            };
            _tipTimer.Start();
        }

        private void RebuildSwatches()
        {
            _swatchFlow.Controls.Clear();
            _paletteCountLbl.Text = $"{_palette.Count} color{(_palette.Count == 1 ? "" : "s")}";

            foreach (var c in _palette)
            {
                var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                var hexOnly = $"{c.R:X2}{c.G:X2}{c.B:X2}";

                var swatch = new Panel
                {
                    Size = new Size(220, 48),
                    BackColor = Color.FromArgb(40, 40, 48),
                    Margin = new Padding(0, 0, 0, 4),
                    Cursor = Cursors.Hand,
                    Tag = c,
                };

                var colorBox = new Panel
                {
                    Size = new Size(40, 48),
                    BackColor = c,
                    Location = new Point(0, 0),
                };

                var hexLbl = new Label
                {
                    Text = hex,
                    Font = new Font("Consolas", 9f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(210, 210, 220),
                    Location = new Point(50, 6),
                    AutoSize = true,
                };

                var rgbLbl = new Label
                {
                    Text = $"R:{c.R}  G:{c.G}  B:{c.B}",
                    Font = new Font("Segoe UI", 7.5f),
                    ForeColor = Color.FromArgb(120, 120, 135),
                    Location = new Point(50, 26),
                    AutoSize = true,
                };

                var btnDel = new Label
                {
                    Text = "✕",
                    Font = new Font("Segoe UI", 8f),
                    ForeColor = Color.FromArgb(140, 50, 50),
                    Size = new Size(18, 18),
                    Location = new Point(198, 2),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand,
                    Tag = c,
                };
                btnDel.Click += (_, _) =>
                {
                    _palette.Remove((Color)btnDel.Tag!);
                    RebuildSwatches();
                };

                // Left-click → copy with #   |   Right-click → copy without #
                EventHandler copyWithHash = (_, _) =>
                {
                    Clipboard.SetText(hex);
                    hexLbl.Text = "Copied #!";
                    var t = new System.Windows.Forms.Timer { Interval = 1200 };
                    t.Tick += (_, _) => { hexLbl.Text = hex; t.Stop(); t.Dispose(); };
                    t.Start();
                };

                MouseEventHandler copyNoHash = (_, me) =>
                {
                    if (me.Button != MouseButtons.Right) return;
                    Clipboard.SetText(hexOnly);
                    hexLbl.Text = "Copied!";
                    var t = new System.Windows.Forms.Timer { Interval = 1200 };
                    t.Tick += (_, _) => { hexLbl.Text = hex; t.Stop(); t.Dispose(); };
                    t.Start();
                };

                swatch.Click += copyWithHash;
                colorBox.Click += copyWithHash;
                hexLbl.Click += copyWithHash;
                rgbLbl.Click += copyWithHash;
                swatch.MouseDown += copyNoHash;
                colorBox.MouseDown += copyNoHash;
                hexLbl.MouseDown += copyNoHash;
                rgbLbl.MouseDown += copyNoHash;

                swatch.Controls.AddRange(new Control[] { colorBox, hexLbl, rgbLbl, btnDel });
                _swatchFlow.Controls.Add(swatch);
            }
        }

        private void ExportHex(object? s, EventArgs e)
        {
            var lines = _palette.ConvertAll(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}");
            Clipboard.SetText(string.Join("\n", lines));
        }

        private void ExportPaletteImage(object? s, EventArgs e)
        {
            if (_palette.Count == 0)
            {
                MessageBox.Show("Palette is empty.");
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png",
                FileName = "palette.png"
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            int size = 100;
            int padding = 10;
            int cols = 4;
            int rows = (int)Math.Ceiling(_palette.Count / (double)cols);
            int width = cols * (size + padding) + padding;
            int height = rows * (size + padding) + padding;

            using Bitmap bmp = new Bitmap(width, height);
            using Graphics g = Graphics.FromImage(bmp);

            g.Clear(Color.FromArgb(24, 24, 28));

            for (int i = 0; i < _palette.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                int x = padding + col * (size + padding);
                int y = padding + row * (size + padding);

                using var brush = new SolidBrush(_palette[i]);
                g.FillRectangle(brush, x, y, size, size);
            }

            bmp.Save(dlg.FileName, ImageFormat.Png);
            MessageBox.Show("Palette image saved!");
        }

        private static Button BarBtn(string text, Color bg, int w)
        {
            var b = new Button
            {
                Text = text,
                Width = w,
                Height = 34,
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private void StartEyedropper() { }
        private void ClearPalette() { _palette.Clear(); RebuildSwatches(); }
    }
}