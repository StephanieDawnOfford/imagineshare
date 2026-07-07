using System;
using System.Drawing;
using System.Windows.Forms;

namespace Imageboard_illust
{
    /// <summary>
    /// PNG出力前に解像度・背景色を選ぶダイアログ。
    /// </summary>
    public class ExportOptionsDialog : Form
    {
        public float Scale           { get; private set; } = 1.0f;
        public Color BackgroundColor { get; private set; } = Color.FromArgb(30, 30, 32);

        private readonly int   _baseW, _baseH;
        private readonly Label _sizePreviewLbl;
        private Color          _bgColor = Color.FromArgb(30, 30, 32);
        private readonly Panel _bgSwatch;

        // ── スケール選択肢 ───────────────────────────────────────
        private static readonly (string label, float scale)[] Presets =
        {
            ("0.5x  — 軽量",   0.5f),
            ("1x    — 標準",   1.0f),
            ("1.5x  — 高解像度", 1.5f),
            ("2x    — 2倍",   2.0f),
        };

        private readonly RadioButton[] _radios;

        public ExportOptionsDialog(int baseW, int baseH, string boardName)
        {
            _baseW = baseW;
            _baseH = baseH;

            Text            = "PNG出力オプション";
            Size            = new Size(400, 360);
            MinimumSize     = new Size(400, 360);
            MaximumSize     = new Size(400, 360);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.FromArgb(38, 38, 44);

            // ── タイトル ──────────────────────────────────────────
            var title = new Label
            {
                Text      = $"「{Truncate(boardName, 24)}」を出力",
                ForeColor = Color.FromArgb(210, 210, 220),
                Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location  = new Point(20, 18),
                AutoSize  = true,
            };

            // ── 解像度セクション ───────────────────────────────────
            var scaleHeader = SectionLabel("解像度", 56);

            _radios = new RadioButton[Presets.Length];
            for (int i = 0; i < Presets.Length; i++)
            {
                var (lbl, sc) = Presets[i];
                _radios[i] = new RadioButton
                {
                    Text      = lbl,
                    ForeColor = Color.FromArgb(200, 200, 210),
                    Font      = new Font("Segoe UI", 9.5f),
                    Location  = new Point(28, 80 + i * 28),
                    AutoSize  = true,
                    Checked   = (sc == 1.0f),
                    Tag       = sc,
                };
                _radios[i].CheckedChanged += (_, _) => UpdateSizePreview();
            }

            // サイズプレビューラベル
            _sizePreviewLbl = new Label
            {
                ForeColor = Color.FromArgb(120, 180, 120),
                Font      = new Font("Consolas", 8.5f),
                Location  = new Point(28, 196),
                AutoSize  = true,
            };
            UpdateSizePreview();

            // ── 背景色セクション ───────────────────────────────────
            var bgHeader = SectionLabel("背景色", 226);

            _bgSwatch = new Panel
            {
                Size      = new Size(36, 26),
                Location  = new Point(28, 252),
                BackColor = _bgColor,
                Cursor    = Cursors.Hand,
                BorderStyle = BorderStyle.FixedSingle,
            };
            _bgSwatch.Click += PickBackgroundColor;

            var bgHint = new Label
            {
                Text      = "← クリックして変更",
                ForeColor = Color.FromArgb(130, 130, 145),
                Font      = new Font("Segoe UI", 8.5f),
                Location  = new Point(72, 258),
                AutoSize  = true,
            };

            // プリセット背景色ボタン
            var presetDark  = BgPresetBtn("Dark",  Color.FromArgb(30, 30, 32),  new Point(28,  286));
            var presetWhite = BgPresetBtn("White", Color.White,                  new Point(88,  286));
            var presetTrans = BgPresetBtn("透明",  Color.Transparent,            new Point(148, 286));

            presetDark .Click += (_, _) => SetBgColor(Color.FromArgb(30, 30, 32));
            presetWhite.Click += (_, _) => SetBgColor(Color.White);
            presetTrans.Click += (_, _) => SetBgColor(Color.Transparent);

            // ── OK / キャンセル ───────────────────────────────────
            var btnOk = new Button
            {
                Text      = "✓  PNG出力",
                Width     = 120, Height = 36,
                Location  = new Point(150, 292),
                BackColor = Color.FromArgb(0, 130, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9.5f),
                DialogResult = DialogResult.OK,
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += BtnOk_Click;

            var btnCancel = new Button
            {
                Text      = "キャンセル",
                Width     = 100, Height = 36,
                Location  = new Point(278, 292),
                BackColor = Color.FromArgb(65, 65, 72),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9.5f),
                DialogResult = DialogResult.Cancel,
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[]
            {
                title, scaleHeader,
            });
            foreach (var r in _radios) Controls.Add(r);
            Controls.AddRange(new Control[]
            {
                _sizePreviewLbl,
                bgHeader, _bgSwatch, bgHint,
                presetDark, presetWhite, presetTrans,
                btnOk, btnCancel,
            });
        }

        // ── ヘルパー ─────────────────────────────────────────────

        private static Label SectionLabel(string text, int y) => new()
        {
            Text      = text,
            ForeColor = Color.FromArgb(150, 150, 165),
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location  = new Point(20, y),
            AutoSize  = true,
        };

        private static Button BgPresetBtn(string text, Color color, Point loc)
        {
            var b = new Button
            {
                Text      = text,
                Width     = 52, Height = 24,
                Location  = loc,
                BackColor = color == Color.Transparent ? Color.FromArgb(55, 55, 62) : color,
                ForeColor = color == Color.White ? Color.Black : Color.FromArgb(200, 200, 210),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8f),
                Cursor    = Cursors.Hand,
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 90);
            b.FlatAppearance.BorderSize  = 1;
            return b;
        }

        private void UpdateSizePreview()
        {
            float sc = GetSelectedScale();
            int w = (int)(_baseW * sc);
            int h = (int)(_baseH * sc);
            _sizePreviewLbl.Text = $"出力サイズ: {w} × {h} px";
        }

        private float GetSelectedScale()
        {
            foreach (var r in _radios)
                if (r.Checked) return (float)r.Tag!;
            return 1.0f;
        }

        private void SetBgColor(Color c)
        {
            _bgColor            = c;
            _bgSwatch.BackColor = c == Color.Transparent ? Color.FromArgb(55, 55, 62) : c;
        }

        private void PickBackgroundColor(object? s, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _bgColor == Color.Transparent ? Color.Black : _bgColor, FullOpen = true };
            if (dlg.ShowDialog() == DialogResult.OK) SetBgColor(dlg.Color);
        }

        private void BtnOk_Click(object? s, EventArgs e)
        {
            Scale           = GetSelectedScale();
            BackgroundColor = _bgColor;
            DialogResult    = DialogResult.OK;
        }

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s[..max] + "…";
    }
}
