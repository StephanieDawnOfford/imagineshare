using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Imageboard_illust.Models;

namespace Imageboard_illust
{
    public class ImageItemControl : Panel
    {
        public BoardItem Item { get; }

        private readonly PictureBox _pictureBox;
        private readonly Panel _titleBar;
        private readonly Label _idBadge;
        private readonly Label _titleLabel;
        private readonly Button _closeBtn;

        // ── Resize direction ──────────────────────────────────────────
        private enum ResizeDir { None, N, S, E, W, NE, NW, SE, SW }

        private bool _isDragging;
        private ResizeDir _resizeDir = ResizeDir.None;
        private Point _dragStartScreen;
        private Point _dragStartLocation;
        private Point _resizeStartScreen;
        private Size _resizeOriginalSize;
        private Point _resizeOriginalLocation;

        private const int TitleH = 28;
        private const int EdgeSz = 7;
        private const int CornerSz = 12;
        private const int MinW = 80;
        private const int MinH = TitleH + 40;

        // Original image (used for crop/reset)
        private Image _originalImage;

        public event EventHandler? DeleteRequested;
        public event EventHandler? BringToFrontRequested;
        public event EventHandler? LayoutChanged;
        public event EventHandler? NameChanged;

        // ─────────────────────────────────────────────────────────────
        public ImageItemControl(Image image, BoardItem item)
        {
            Item = item;
            _originalImage = image;

            Size = new Size(item.Width, item.Height);
            Location = new Point(item.X, item.Y);
            BackColor = Color.FromArgb(50, 50, 53);

            // ── Title bar ────────────────────────────────────────────
            _titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = TitleH,
                BackColor = Color.FromArgb(28, 28, 30),
                Cursor = Cursors.SizeAll,
            };

            _idBadge = new Label
            {
                Text = $"#{item.Id:D3}",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 120, 200),
                Font = new Font("Consolas", 7.5f, FontStyle.Bold),
                Width = 40,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.SizeAll,
            };

            _titleLabel = new Label
            {
                Text = item.Name,
                ForeColor = Color.FromArgb(210, 210, 210),
                Font = new Font("Segoe UI", 8.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0),
                Cursor = Cursors.SizeAll,
            };
            _titleLabel.DoubleClick += (_, _) => BeginRename();

            _closeBtn = new Button
            {
                Text = "✕",
                Width = 26,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(185, 35, 22),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f),
                Cursor = Cursors.Hand,
            };
            _closeBtn.FlatAppearance.BorderSize = 0;
            _closeBtn.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);

            _titleBar.Controls.Add(_titleLabel);
            _titleBar.Controls.Add(_idBadge);
            _titleBar.Controls.Add(_closeBtn);

            // ── Picture box ──────────────────────────────────────────
            _pictureBox = new PictureBox
            {
                Image = image,
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 38, 40),
                Cursor = Cursors.SizeAll,
            };

            Controls.Add(_pictureBox);
            Controls.Add(_titleBar);

            // ── Context menu ─────────────────────────────────────────
            var ctx = new ContextMenuStrip();
            ctx.Items.Add("Rename…", null, (_, _) => BeginRename());
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("✂  Crop…", null, (_, _) => OpenCropDialog());
            ctx.Items.Add("Reset crop", null, (_, _) => ResetCrop());
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("Bring to front", null, (_, _) => BringToFrontRequested?.Invoke(this, EventArgs.Empty));
            ctx.Items.Add("Send to back", null, (_, _) => SendToBack());
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("Save image as…", null, OnExportImage);
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("Delete", null, (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty));
            foreach (Control c in new Control[] { this, _titleBar, _idBadge, _titleLabel, _pictureBox })
                c.ContextMenuStrip = ctx;

            // ── Mouse events ─────────────────────────────────────────
            foreach (Control c in new Control[] { _titleBar, _titleLabel, _idBadge })
            {
                c.MouseDown += TitleBar_Down;
                c.MouseMove += TitleBar_Move;
                c.MouseUp += Mouse_Up;
            }

            _pictureBox.MouseDown += Content_Down;
            _pictureBox.MouseMove += Content_Move;
            _pictureBox.MouseUp += Mouse_Up;

            MouseDown += Panel_Down;
            MouseMove += Panel_Move;
            MouseUp += Mouse_Up;

            Paint += OnPaint;
        }

        // ══════════════ Resize direction detection ═══════════════════

        // Determine which edge or corner the point is near (panel-local coords)
        private ResizeDir GetResizeDirFromPanel(Point p)
        {
            int x = p.X, y = p.Y, w = Width, h = Height;

            bool nearL = x <= CornerSz;
            bool nearR = x >= w - CornerSz;
            bool nearT = y <= CornerSz;
            bool nearB = y >= h - CornerSz;

            if (nearT && nearL) return ResizeDir.NW;
            if (nearT && nearR) return ResizeDir.NE;
            if (nearB && nearL) return ResizeDir.SW;
            if (nearB && nearR) return ResizeDir.SE;
            if (y <= EdgeSz) return ResizeDir.N;
            if (y >= h - EdgeSz) return ResizeDir.S;
            if (x <= EdgeSz) return ResizeDir.W;
            if (x >= w - EdgeSz) return ResizeDir.E;
            return ResizeDir.None;
        }

        private static Cursor DirToCursor(ResizeDir d) => d switch
        {
            ResizeDir.N or ResizeDir.S => Cursors.SizeNS,
            ResizeDir.E or ResizeDir.W => Cursors.SizeWE,
            ResizeDir.NW or ResizeDir.SE => Cursors.SizeNWSE,
            ResizeDir.NE or ResizeDir.SW => Cursors.SizeNESW,
            _ => Cursors.SizeAll,
        };

        // ══════════════ Title bar — move ════════════════════════════

        private void TitleBar_Down(object? s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            BringToFrontRequested?.Invoke(this, EventArgs.Empty);
            _isDragging = true;
            _dragStartScreen = Scr(s, e.Location);
            _dragStartLocation = Location;
        }

        private void TitleBar_Move(object? s, MouseEventArgs e)
        {
            if (!_isDragging || Parent == null) return;
            var sc = Scr(s, e.Location);
            Location = new Point(
                Math.Max(0, _dragStartLocation.X + sc.X - _dragStartScreen.X),
                Math.Max(0, _dragStartLocation.Y + sc.Y - _dragStartScreen.Y));
        }

        // ══════════════ Image area — move or edge resize ═════════════

        private void Content_Down(object? s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            BringToFrontRequested?.Invoke(this, EventArgs.Empty);

            // Convert PictureBox local coords → panel local coords
            var panelPt = new Point(
                _pictureBox.Left + e.Location.X,
                _pictureBox.Top + e.Location.Y);

            var dir = GetResizeDirFromPanel(panelPt);

            if (dir != ResizeDir.None)
                StartResize(dir, Scr(s, e.Location));
            else
            {
                // Centre area → move
                _isDragging = true;
                _dragStartScreen = Scr(s, e.Location);
                _dragStartLocation = Location;
            }
        }

        private void Content_Move(object? s, MouseEventArgs e)
        {
            var panelPt = new Point(
                _pictureBox.Left + e.Location.X,
                _pictureBox.Top + e.Location.Y);

            if (_isDragging && Parent != null)
            {
                var sc = Scr(s, e.Location);
                Location = new Point(
                    Math.Max(0, _dragStartLocation.X + sc.X - _dragStartScreen.X),
                    Math.Max(0, _dragStartLocation.Y + sc.Y - _dragStartScreen.Y));
                return;
            }

            if (_resizeDir != ResizeDir.None)
            {
                ApplyResize(Scr(s, e.Location));
                return;
            }

            // Update hover cursor
            var dir = GetResizeDirFromPanel(panelPt);
            _pictureBox.Cursor = dir == ResizeDir.None ? Cursors.SizeAll : DirToCursor(dir);
        }

        // ══════════════ Panel itself — thin edge area ════════════════

        private void Panel_Down(object? s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var dir = GetResizeDirFromPanel(e.Location);
            if (dir != ResizeDir.None)
                StartResize(dir, Scr(s, e.Location));
        }

        private void Panel_Move(object? s, MouseEventArgs e)
        {
            if (_resizeDir != ResizeDir.None)
                ApplyResize(Scr(s, e.Location));
            else
            {
                var dir = GetResizeDirFromPanel(e.Location);
                Cursor = dir == ResizeDir.None ? Cursors.Default : DirToCursor(dir);
            }
        }

        // ══════════════ Resize shared logic ═════════════════════════

        private void StartResize(ResizeDir dir, Point sc)
        {
            _resizeDir = dir;
            _resizeStartScreen = sc;
            _resizeOriginalSize = Size;
            _resizeOriginalLocation = Location;
            Cursor = DirToCursor(dir);
        }

        private void ApplyResize(Point sc)
        {
            if (_resizeDir == ResizeDir.None) return;
            int dx = sc.X - _resizeStartScreen.X;
            int dy = sc.Y - _resizeStartScreen.Y;

            int newX = _resizeOriginalLocation.X, newY = _resizeOriginalLocation.Y;
            int newW = _resizeOriginalSize.Width, newH = _resizeOriginalSize.Height;

            if (_resizeDir is ResizeDir.E or ResizeDir.NE or ResizeDir.SE)
                newW = Math.Max(MinW, _resizeOriginalSize.Width + dx);
            if (_resizeDir is ResizeDir.W or ResizeDir.NW or ResizeDir.SW)
            { newW = Math.Max(MinW, _resizeOriginalSize.Width - dx); newX = _resizeOriginalLocation.X + (_resizeOriginalSize.Width - newW); }
            if (_resizeDir is ResizeDir.S or ResizeDir.SE or ResizeDir.SW)
                newH = Math.Max(MinH, _resizeOriginalSize.Height + dy);
            if (_resizeDir is ResizeDir.N or ResizeDir.NE or ResizeDir.NW)
            { newH = Math.Max(MinH, _resizeOriginalSize.Height - dy); newY = _resizeOriginalLocation.Y + (_resizeOriginalSize.Height - newH); }

            Location = new Point(Math.Max(0, newX), Math.Max(0, newY));
            Size = new Size(newW, newH);
        }

        // ══════════════ Mouse up ═════════════════════════════════════

        private void Mouse_Up(object? s, MouseEventArgs e)
        {
            bool was = _isDragging || _resizeDir != ResizeDir.None;
            _isDragging = false;
            _resizeDir = ResizeDir.None;
            Cursor = Cursors.Default;
            _pictureBox.Cursor = Cursors.SizeAll;

            if (was)
            {
                Item.X = Location.X;
                Item.Y = Location.Y;
                Item.Width = Width;
                Item.Height = Height;
                LayoutChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // ══════════════ Painting ══════════════════════════════════════

        private void OnPaint(object? s, PaintEventArgs e)
        {
            var g = e.Graphics;
            using var bp = new Pen(Color.FromArgb(90, 90, 96), 1);
            g.DrawRectangle(bp, 0, 0, Width - 1, Height - 1);

            // L-shaped corner handles
            using var hp = new Pen(Color.FromArgb(160, 160, 170), 1);
            int cs = CornerSz;
            g.DrawLine(hp, 0, cs, 0, 0); g.DrawLine(hp, 0, 0, cs, 0);
            g.DrawLine(hp, Width - cs, 0, Width - 1, 0); g.DrawLine(hp, Width - 1, 0, Width - 1, cs);
            g.DrawLine(hp, 0, Height - cs, 0, Height - 1); g.DrawLine(hp, 0, Height - 1, cs, Height - 1);
            g.DrawLine(hp, Width - cs, Height - 1, Width - 1, Height - 1); g.DrawLine(hp, Width - 1, Height - cs, Width - 1, Height - 1);
        }

        // ══════════════ Crop ══════════════════════════════════════════

        private void OpenCropDialog()
        {
            using var dlg = new CropDialog(_originalImage);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var cropped = dlg.CroppedImage;
            if (cropped == null) return;
            _pictureBox.Image = cropped;
            Invalidate();
        }

        private void ResetCrop()
        {
            _pictureBox.Image = _originalImage;
            Invalidate();
        }

        /// <summary>Replace the original image from outside (called when restoring from DB).</summary>
        public void SetOriginalImage(Image img)
        {
            _originalImage = img;
            _pictureBox.Image = img;
        }

        // ══════════════ Rename ════════════════════════════════════════

        public void BeginRename()
        {
            var tb = new TextBox
            {
                Text = Item.Name,
                Font = _titleLabel.Font,
                BackColor = Color.FromArgb(40, 55, 80),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
            };
            _titleLabel.Visible = false;
            _titleBar.Controls.Add(tb);
            tb.BringToFront(); tb.SelectAll(); tb.Focus();

            bool done = false;
            void Commit()
            {
                if (done) return; done = true;
                var n = tb.Text.Trim();
                if (n.Length == 0) n = $"Image #{Item.Id:D3}";
                Item.Name = n; _titleLabel.Text = n; _titleLabel.Visible = true;
                _titleBar.Controls.Remove(tb); tb.Dispose();
                NameChanged?.Invoke(this, EventArgs.Empty);
            }
            tb.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Commit(); }
                if (e.KeyCode == Keys.Escape) { done = true; _titleLabel.Visible = true; _titleBar.Controls.Remove(tb); tb.Dispose(); }
            };
            tb.LostFocus += (_, _) => { if (!tb.IsDisposed) Commit(); };
        }

        // ══════════════ Export ════════════════════════════════════════

        private void OnExportImage(object? s, EventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Save image as",
                FileName = Item.Name,
                Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp",
                DefaultExt = "png",
            };
            if (dlg.ShowDialog() != DialogResult.OK || _pictureBox.Image == null) return;
            var fmt = dlg.FilterIndex switch
            {
                2 => System.Drawing.Imaging.ImageFormat.Jpeg,
                3 => System.Drawing.Imaging.ImageFormat.Bmp,
                _ => System.Drawing.Imaging.ImageFormat.Png,
            };
            _pictureBox.Image.Save(dlg.FileName, fmt);
        }

        // ══════════════ Utilities ════════════════════════════════════

        private static Point Scr(object? s, Point p) =>
            (s as Control)?.PointToScreen(p) ?? p;

        public void RefreshTitle()
        {
            _idBadge.Text = $"#{Item.Id:D3}";
            _titleLabel.Text = Item.Name;
        }

        public void ForwardMouseWheelTo(Control target, MouseEventHandler handler)
        {
            foreach (Control c in new Control[] { this, _titleBar, _idBadge, _titleLabel, _pictureBox })
                c.MouseWheel += handler;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Crop Dialog
    // ════════════════════════════════════════════════════════════════
    public class CropDialog : Form
    {
        public Image? CroppedImage { get; private set; }

        private readonly Image _src;
        private readonly Panel _previewPanel;
        private readonly Label _infoLabel;

        // Crop rectangle in preview coordinates
        private Rectangle _cropRect;
        private bool _dragging;
        private Point _dragStart;
        private Rectangle _dragStartRect;

        private enum CropHandle { None, Move, TL, TR, BL, BR, T, B, L, R }
        private CropHandle _activeHandle = CropHandle.None;

        // The area inside the preview panel where the image is drawn
        private Rectangle _imgRect;

        public CropDialog(Image src)
        {
            _src = src;

            Text = "Crop Image";
            Size = new Size(820, 640);
            MinimumSize = new Size(500, 400);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(32, 32, 35);
            FormBorderStyle = FormBorderStyle.Sizable;

            // Preview panel
            _previewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 26),
            };
            _previewPanel.Paint += Preview_Paint;
            _previewPanel.MouseDown += Preview_MouseDown;
            _previewPanel.MouseMove += Preview_MouseMove;
            _previewPanel.MouseUp += Preview_MouseUp;
            _previewPanel.Resize += (_, _) => { RecalcImgRect(); _previewPanel.Invalidate(); };

            // Button bar
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = Color.FromArgb(37, 37, 40),
            };

            _infoLabel = new Label
            {
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true,
                Location = new Point(12, 15),
            };

            var btnOk = new Button
            {
                Text = "✓  Apply Crop",
                Width = 160,
                Height = 34,
                Location = new Point(bar.Width - 340, 7),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(0, 130, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                DialogResult = DialogResult.OK,
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += BtnOk_Click;

            var btnCancel = new Button
            {
                Text = "Cancel",
                Width = 100,
                Height = 34,
                Location = new Point(bar.Width - 115, 7),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(70, 70, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                DialogResult = DialogResult.Cancel,
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            bar.Controls.AddRange(new Control[] { _infoLabel, btnOk, btnCancel });

            Controls.Add(_previewPanel);
            Controls.Add(bar);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Load += (_, _) =>
            {
                RecalcImgRect();
                // Default crop = full image
                _cropRect = _imgRect;
                UpdateInfo();
                _previewPanel.Invalidate();
            };
        }

        // ── Calculate the image drawing area inside the preview panel ──

        private void RecalcImgRect()
        {
            if (_previewPanel.Width == 0 || _previewPanel.Height == 0) return;
            double r = Math.Min(
                (double)(_previewPanel.Width - 40) / _src.Width,
                (double)(_previewPanel.Height - 40) / _src.Height);
            int pw = (int)(_src.Width * r);
            int ph = (int)(_src.Height * r);
            int px = (_previewPanel.Width - pw) / 2;
            int py = (_previewPanel.Height - ph) / 2;
            _imgRect = new Rectangle(px, py, pw, ph);
        }

        // ── Painting ─────────────────────────────────────────────────

        private void Preview_Paint(object? s, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Draw image
            g.DrawImage(_src, _imgRect);

            // Dark overlay outside the crop rect
            using var darkBrush = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
            var outer = new System.Drawing.Region(_imgRect);
            outer.Exclude(_cropRect);
            g.FillRegion(darkBrush, outer);

            // Crop border
            using var cropPen = new Pen(Color.FromArgb(255, 255, 255), 1.5f);
            g.DrawRectangle(cropPen, _cropRect);

            // Rule-of-thirds grid
            using var gridPen = new Pen(Color.FromArgb(100, 255, 255, 255), 0.8f);
            for (int i = 1; i <= 2; i++)
            {
                int gx = _cropRect.X + _cropRect.Width * i / 3;
                int gy = _cropRect.Y + _cropRect.Height * i / 3;
                g.DrawLine(gridPen, gx, _cropRect.Y, gx, _cropRect.Bottom);
                g.DrawLine(gridPen, _cropRect.X, gy, _cropRect.Right, gy);
            }

            // Corner handles (small white squares)
            foreach (var hp in GetHandleRects())
                g.FillRectangle(Brushes.White, hp.Value);
        }

        // ── Handle rectangles ────────────────────────────────────────

        private Dictionary<CropHandle, Rectangle> GetHandleRects()
        {
            const int hs = 8;
            int cx = _cropRect.X, cy = _cropRect.Y;
            int cw = _cropRect.Width, ch = _cropRect.Height;
            int mx = cx + cw / 2 - hs / 2, my = cy + ch / 2 - hs / 2;
            return new Dictionary<CropHandle, Rectangle>
            {
                { CropHandle.TL, new Rectangle(cx - hs/2,         cy - hs/2,         hs, hs) },
                { CropHandle.TR, new Rectangle(cx + cw - hs/2,    cy - hs/2,         hs, hs) },
                { CropHandle.BL, new Rectangle(cx - hs/2,         cy + ch - hs/2,    hs, hs) },
                { CropHandle.BR, new Rectangle(cx + cw - hs/2,    cy + ch - hs/2,    hs, hs) },
                { CropHandle.T,  new Rectangle(mx,                 cy - hs/2,         hs, hs) },
                { CropHandle.B,  new Rectangle(mx,                 cy + ch - hs/2,    hs, hs) },
                { CropHandle.L,  new Rectangle(cx - hs/2,         my,                 hs, hs) },
                { CropHandle.R,  new Rectangle(cx + cw - hs/2,    my,                 hs, hs) },
            };
        }

        private CropHandle HitHandle(Point p)
        {
            foreach (var kv in GetHandleRects())
            {
                var r = kv.Value; r.Inflate(4, 4);
                if (r.Contains(p)) return kv.Key;
            }
            return CropHandle.None;
        }

        // ── Mouse events ─────────────────────────────────────────────

        private void Preview_MouseDown(object? s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _dragStart = e.Location;
            _dragStartRect = _cropRect;
            _activeHandle = HitHandle(e.Location);

            if (_activeHandle == CropHandle.None && _cropRect.Contains(e.Location))
                _activeHandle = CropHandle.Move;

            _dragging = true;
        }

        private void Preview_MouseMove(object? s, MouseEventArgs e)
        {
            // Update cursor when not dragging
            if (!_dragging)
            {
                var h = HitHandle(e.Location);
                _previewPanel.Cursor = h switch
                {
                    CropHandle.TL or CropHandle.BR => Cursors.SizeNWSE,
                    CropHandle.TR or CropHandle.BL => Cursors.SizeNESW,
                    CropHandle.T or CropHandle.B => Cursors.SizeNS,
                    CropHandle.L or CropHandle.R => Cursors.SizeWE,
                    _ => _cropRect.Contains(e.Location) ? Cursors.SizeAll : Cursors.Default,
                };
                return;
            }

            int dx = e.X - _dragStart.X;
            int dy = e.Y - _dragStart.Y;
            var r = _dragStartRect;

            var next = _activeHandle switch
            {
                CropHandle.Move => new Rectangle(
                    Math.Clamp(r.X + dx, _imgRect.X, _imgRect.Right - r.Width),
                    Math.Clamp(r.Y + dy, _imgRect.Y, _imgRect.Bottom - r.Height),
                    r.Width, r.Height),

                CropHandle.TL => RectFromPoints(r.Right, r.Bottom, e.X, e.Y),
                CropHandle.TR => RectFromPoints(r.Left, r.Bottom, e.X, e.Y),
                CropHandle.BL => RectFromPoints(r.Right, r.Top, e.X, e.Y),
                CropHandle.BR => RectFromPoints(r.Left, r.Top, e.X, e.Y),
                CropHandle.T => RectFromPoints(r.Left, r.Bottom, r.Right, e.Y),
                CropHandle.B => RectFromPoints(r.Left, r.Top, r.Right, e.Y),
                CropHandle.L => RectFromPoints(r.Right, r.Top, e.X, r.Bottom),
                CropHandle.R => RectFromPoints(r.Left, r.Top, e.X, r.Bottom),
                _ => _cropRect,
            };

            // Clamp to image bounds
            _cropRect = Rectangle.Intersect(next, _imgRect);
            if (_cropRect.Width < 4) _cropRect.Width = 4;
            if (_cropRect.Height < 4) _cropRect.Height = 4;

            UpdateInfo();
            _previewPanel.Invalidate();
        }

        private void Preview_MouseUp(object? s, MouseEventArgs e)
        {
            _dragging = false;
            _activeHandle = CropHandle.None;
        }

        // ── Utilities ────────────────────────────────────────────────

        private static Rectangle RectFromPoints(int x1, int y1, int x2, int y2) =>
            new Rectangle(
                Math.Min(x1, x2), Math.Min(y1, y2),
                Math.Abs(x2 - x1), Math.Abs(y2 - y1));

        private void UpdateInfo()
        {
            // Convert preview coords → original image coords for the info label
            if (_imgRect.Width == 0 || _imgRect.Height == 0) return;
            double scaleX = (double)_src.Width / _imgRect.Width;
            double scaleY = (double)_src.Height / _imgRect.Height;
            int sw = (int)((_cropRect.X - _imgRect.X) * scaleX);
            int sy = (int)((_cropRect.Y - _imgRect.Y) * scaleY);
            int ew = (int)(_cropRect.Width * scaleX);
            int eh = (int)(_cropRect.Height * scaleY);
            _infoLabel.Text = $"Crop region: {sw}, {sy}  →  {ew} × {eh} px";
        }

        private void BtnOk_Click(object? s, EventArgs e)
        {
            if (_imgRect.Width == 0) return;

            double scaleX = (double)_src.Width / _imgRect.Width;
            double scaleY = (double)_src.Height / _imgRect.Height;

            var srcRect = new Rectangle(
                (int)((_cropRect.X - _imgRect.X) * scaleX),
                (int)((_cropRect.Y - _imgRect.Y) * scaleY),
                (int)(_cropRect.Width * scaleX),
                (int)(_cropRect.Height * scaleY));

            srcRect.X = Math.Clamp(srcRect.X, 0, _src.Width - 1);
            srcRect.Y = Math.Clamp(srcRect.Y, 0, _src.Height - 1);
            srcRect.Width = Math.Clamp(srcRect.Width, 1, _src.Width - srcRect.X);
            srcRect.Height = Math.Clamp(srcRect.Height, 1, _src.Height - srcRect.Y);

            var bmp = new Bitmap(srcRect.Width, srcRect.Height);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_src,
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                srcRect,
                GraphicsUnit.Pixel);

            CroppedImage = bmp;
            DialogResult = DialogResult.OK;
        }
    }
}