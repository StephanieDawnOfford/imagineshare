using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Imageboard_illust.Models;

namespace Imageboard_illust
{
    /// <summary>
    /// Landing page shown when the app launches.
    /// The user chooses between Image Board and Color Picker.
    /// </summary>
    public class StartPage : Panel
    {
        public event EventHandler? ImageBoardChosen;
        public event EventHandler? ColorPickerChosen;
        public event EventHandler? LogoutRequested;

        private readonly User? _currentUser;

        public StartPage(User? user = null)
        {
            _currentUser = user;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(22, 22, 26);

            // ── Background gradient ───────────────────────────────
            Paint += (_, e) =>
            {
                using var br = new LinearGradientBrush(
                    ClientRectangle,
                    Color.FromArgb(22, 22, 28),
                    Color.FromArgb(30, 28, 38),
                    LinearGradientMode.ForwardDiagonal);
                e.Graphics.FillRectangle(br, ClientRectangle);

                // Subtle dot grid overlay
                using var dotBrush = new SolidBrush(Color.FromArgb(35, 255, 255, 255));
                const int gap = 32;
                for (int x = gap; x < Width; x += gap)
                    for (int y = gap; y < Height; y += gap)
                        e.Graphics.FillEllipse(dotBrush, x - 1, y - 1, 2, 2);
            };

            // ── User info and logout button (top right) ───────────
            if (_currentUser != null)
            {
                var userPanel = new Panel
                {
                    Size = new Size(300, 60),
                    BackColor = Color.Transparent,
                    Location = new Point(0, 0)
                };

                var userEmail = new Label
                {
                    Text = $"Logged in as: {_currentUser.Email}",
                    ForeColor = Color.FromArgb(150, 150, 160),
                    Font = new Font("Segoe UI", 9f),
                    AutoSize = true,
                    Location = new Point(0, 10)
                };

                var logoutBtn = new Button
                {
                    Text = "Logout",
                    Size = new Size(80, 30),
                    Location = new Point(0, 28),
                    BackColor = Color.FromArgb(80, 80, 90),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9f),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                logoutBtn.Click += (_, _) => LogoutRequested?.Invoke(this, EventArgs.Empty);

                userPanel.Controls.Add(userEmail);
                userPanel.Controls.Add(logoutBtn);
                Controls.Add(userPanel);

                Resize += (_, _) =>
                {
                    userPanel.Location = new Point(Width - 300, 10);
                };
            }

            // ── Centre container ──────────────────────────────────
            var center = new Panel
            {
                Width = 720,
                Height = 480,
                BackColor = Color.Transparent,
            };

            // Title
            var titleLbl = new Label
            {
                Text = "Image Board Studio",
                ForeColor = Color.FromArgb(230, 230, 235),
                Font = new Font("Segoe UI", 26f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 0),
            };

            // Subtitle
            var subLbl = new Label
            {
                Text = "Choose a mode to get started",
                ForeColor = Color.FromArgb(120, 120, 135),
                Font = new Font("Segoe UI", 11f),
                AutoSize = true,
                Location = new Point(4, 52),
            };

            // Divider line
            var line = new Panel
            {
                Location = new Point(0, 90),
                Size = new Size(720, 1),
                BackColor = Color.FromArgb(55, 55, 65),
            };

            // ── Two mode cards ────────────────────────────────────
            var cardBoard = ModeCard(
                "🖼",
                "Image Board",
                "Collect images and build a visual board.\nPerfect for mood boards, references, and collections.",
                Color.FromArgb(0, 100, 180),
                Color.FromArgb(0, 130, 220));

            var cardColor = ModeCard(
                "🎨",
                "Color Picker",
                "Pick colors from images with an eyedropper\nand save them to a reusable palette.",
                Color.FromArgb(120, 60, 160),
                Color.FromArgb(150, 80, 200));

            cardBoard.Location = new Point(0, 116);
            cardColor.Location = new Point(380, 116);

            cardBoard.Click += (_, _) => ImageBoardChosen?.Invoke(this, EventArgs.Empty);
            cardColor.Click += (_, _) => ColorPickerChosen?.Invoke(this, EventArgs.Empty);

            // Propagate clicks to all child controls inside each card
            PropagateClick(cardBoard, (_, _) => ImageBoardChosen?.Invoke(this, EventArgs.Empty));
            PropagateClick(cardColor, (_, _) => ColorPickerChosen?.Invoke(this, EventArgs.Empty));

            // Footer hint
            var footer = new Label
            {
                Text = "💡  You can switch modes at any time using the Home button.",
                ForeColor = Color.FromArgb(80, 80, 95),
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true,
                Location = new Point(0, 440),
            };

            center.Controls.AddRange(new Control[]
                { titleLbl, subLbl, line, cardBoard, cardColor, footer });

            Controls.Add(center);

            // Re-centre the container whenever the window is resized
            Resize += (_, _) => CenterControl(center);
            CenterControl(center);
        }

        // ── Mode card factory ─────────────────────────────────────

        private static Panel ModeCard(string icon, string title, string desc,
                                      Color borderColor, Color hoverColor)
        {
            var card = new Panel
            {
                Width = 340,
                Height = 280,
                BackColor = Color.FromArgb(36, 36, 42),
                Cursor = Cursors.Hand,
            };

            // Draw rounded-rect border
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var rc = ((Panel)s!).ClientRectangle;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Rounded background fill
                using var bgBrush = new SolidBrush(card.BackColor);
                FillRoundRect(g, bgBrush, rc, 12);

                // Border
                using var pen = new Pen(borderColor, 1.5f);
                DrawRoundRect(g, pen, rc, 12);
            };

            // Hover effect
            card.MouseEnter += (_, _) =>
            {
                card.BackColor = Color.FromArgb(44, 44, 52);
                card.Invalidate();
            };
            card.MouseLeave += (_, _) =>
            {
                card.BackColor = Color.FromArgb(36, 36, 42);
                card.Invalidate();
            };

            // Icon
            var iconLbl = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI Emoji", 42f),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(340, 80),
                Location = new Point(0, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };

            // Title label
            var titleLbl = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 220, 230),
                AutoSize = false,
                Size = new Size(300, 32),
                Location = new Point(20, 118),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };

            // Description label
            var descLbl = new Label
            {
                Text = desc,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(130, 130, 145),
                AutoSize = false,
                Size = new Size(300, 60),
                Location = new Point(20, 158),
                TextAlign = ContentAlignment.TopCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };

            // "Open" call-to-action label
            var openLbl = new Label
            {
                Text = "Open  →",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(hoverColor.R, hoverColor.G, hoverColor.B),
                AutoSize = false,
                Size = new Size(300, 28),
                Location = new Point(20, 232),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };

            card.Controls.AddRange(new Control[] { iconLbl, titleLbl, descLbl, openLbl });
            return card;
        }

        private static void PropagateClick(Control parent, EventHandler handler)
        {
            foreach (Control c in parent.Controls)
            {
                c.Click += handler;
                PropagateClick(c, handler);
            }
        }

        private void CenterControl(Control c)
        {
            c.Location = new Point(
                Math.Max(0, (Width - c.Width) / 2),
                Math.Max(0, (Height - c.Height) / 2));
        }

        // ── Rounded rectangle drawing helpers ────────────────────

        private static void FillRoundRect(Graphics g, Brush br, Rectangle rc, int r)
        {
            using var path = RoundRectPath(rc, r);
            g.FillPath(br, path);
        }

        private static void DrawRoundRect(Graphics g, Pen pen, Rectangle rc, int r)
        {
            using var path = RoundRectPath(rc, r);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath RoundRectPath(Rectangle rc, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(rc.X, rc.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rc.Right - r * 2, rc.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rc.Right - r * 2, rc.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rc.X, rc.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}