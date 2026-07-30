using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Imageboard_illust
{
    public class IntroPage : Panel
    {
        public event EventHandler? ContinueRequested;

        public IntroPage()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(18, 18, 24);

            Paint += (_, e) =>
            {
                using var br = new LinearGradientBrush(
                    ClientRectangle,
                    Color.FromArgb(16, 18, 26),
                    Color.FromArgb(24, 24, 36),
                    LinearGradientMode.BackwardDiagonal);
                e.Graphics.FillRectangle(br, ClientRectangle);

                using var dotBrush = new SolidBrush(Color.FromArgb(22, 255, 255, 255));
                const int gap = 40;
                for (int x = gap; x < Width; x += gap)
                    for (int y = gap; y < Height; y += gap)
                        e.Graphics.FillEllipse(dotBrush, x - 1, y - 1, 2, 2);
            };

            var center = new Panel
            {
                Width = 980,
                Height = 520,
                BackColor = Color.Transparent,
            };

            var titleLabel = new Label
            {
                Text = "Welcome to Image Board Studio",
                ForeColor = Color.FromArgb(245, 245, 250),
                Font = new Font("Segoe UI", 34f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 0),
            };

            var subtitleLabel = new Label
            {
                Text = "Organize your images, collaborate on shared boards, and capture colors from any reference.",
                ForeColor = Color.FromArgb(185, 190, 210),
                Font = new Font("Segoe UI", 12f),
                AutoSize = true,
                MaximumSize = new Size(920, 0),
                Location = new Point(0, 60),
            };

            var continueButton = new Button
            {
                Text = "Get Started",
                Size = new Size(180, 46),
                Location = new Point(0, 150),
                BackColor = Color.FromArgb(0, 130, 230),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            continueButton.FlatAppearance.BorderSize = 0;
            continueButton.Click += (_, _) => ContinueRequested?.Invoke(this, EventArgs.Empty);

            var featurePanel = new FlowLayoutPanel
            {
                Location = new Point(0, 220),
                Size = new Size(940, 240),
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
            };

            featurePanel.Controls.Add(FeatureCard("🖼", "Individual Boards", "Create private boards for your own mood boards, references, and collections.", Color.FromArgb(0, 120, 220)));
            featurePanel.Controls.Add(FeatureCard("🤝", "Shared Boards", "Collaborate with teammates using shared boards and invite links.", Color.FromArgb(0, 170, 120)));
            featurePanel.Controls.Add(FeatureCard("🎨", "Color Picker", "Pull colors from images and save them to a reusable palette.", Color.FromArgb(145, 75, 210)));

            center.Controls.Add(titleLabel);
            center.Controls.Add(subtitleLabel);
            center.Controls.Add(continueButton);
            center.Controls.Add(featurePanel);
            Controls.Add(center);

            Resize += (_, _) => CenterControl(center);
            CenterControl(center);
        }

        private static Panel FeatureCard(string icon, string title, string text, Color accent)
        {
            var card = new Panel
            {
                Size = new Size(300, 220),
                BackColor = Color.FromArgb(30, 30, 38),
            };

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.FromArgb(70, 70, 90), 1.2f);
                var rc = card.ClientRectangle;
                g.DrawRectangle(pen, 0, 0, rc.Width - 1, rc.Height - 1);
            };

            var iconLabel = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI Emoji", 28f),
                ForeColor = accent,
                AutoSize = true,
                Location = new Point(20, 20),
                BackColor = Color.Transparent,
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 70),
            };

            var descLabel = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(170, 175, 190),
                AutoSize = false,
                Size = new Size(260, 100),
                Location = new Point(20, 100),
            };

            card.Controls.Add(iconLabel);
            card.Controls.Add(titleLabel);
            card.Controls.Add(descLabel);
            return card;
        }

        private void CenterControl(Control c)
        {
            c.Location = new Point(
                Math.Max(0, (Width - c.Width) / 2),
                Math.Max(0, (Height - c.Height) / 2));
        }
    }
}
