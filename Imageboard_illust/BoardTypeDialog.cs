using System;
using System.Windows.Forms;
using System.Drawing;

namespace Imageboard_illust
{
    /// <summary>
    /// Dialog to select board type after clicking "Image Board"
    /// Users can choose between Individual or Shared board
    /// </summary>
    public class BoardTypeDialog : Form
    {
        public event EventHandler<BoardTypeEventArgs>? BoardTypeSelected;

        private RadioButton _individualRadio = null!;
        private RadioButton _sharedRadio = null!;
        private RadioButton _privateRadio = null!;
        private RadioButton _publicRadio = null!;
        private Button _continueButton = null!;

        public BoardTypeDialog()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text = "Select Board Type";
            Size = new Size(450, 350);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(22, 22, 26);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10);
            MaximizeBox = false;
            MinimizeBox = false;

            // Title
            var titleLabel = new Label
            {
                Text = "What type of board would you like to create?",
                AutoSize = true,
                Location = new Point(20, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            Controls.Add(titleLabel);

            // Individual Board Section
            var individualGroupBox = new GroupBox
            {
                Text = "Individual Board",
                Location = new Point(20, 60),
                Size = new Size(400, 120),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(22, 22, 26),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            _individualRadio = new RadioButton
            {
                Text = "Individual Board",
                Location = new Point(20, 25),
                Checked = true,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(22, 22, 26),
                AutoSize = true
            };
            _individualRadio.CheckedChanged += IndividualRadio_CheckedChanged;

            var individualDescLabel = new Label
            {
                Text = "Personal workspace. You control who can access it.",
                Location = new Point(40, 50),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9)
            };

            _privateRadio = new RadioButton
            {
                Text = "Private (only you)",
                Location = new Point(60, 75),
                Checked = true,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(22, 22, 26),
                AutoSize = true,
                Enabled = true
            };

            _publicRadio = new RadioButton
            {
                Text = "Public (others can view)",
                Location = new Point(60, 100),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(22, 22, 26),
                AutoSize = true,
                Enabled = true
            };

            individualGroupBox.Controls.Add(_individualRadio);
            individualGroupBox.Controls.Add(individualDescLabel);
            individualGroupBox.Controls.Add(_privateRadio);
            individualGroupBox.Controls.Add(_publicRadio);
            Controls.Add(individualGroupBox);

            // Shared Board Section
            var sharedGroupBox = new GroupBox
            {
                Text = "Shared Board",
                Location = new Point(20, 190),
                Size = new Size(400, 70),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(22, 22, 26),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            _sharedRadio = new RadioButton
            {
                Text = "Shared Board - Collaborate with others",
                Location = new Point(20, 25),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(22, 22, 26),
                AutoSize = true
            };
            _sharedRadio.CheckedChanged += SharedRadio_CheckedChanged;

            var sharedDescLabel = new Label
            {
                Text = "Work together with multiple users on the same board.",
                Location = new Point(40, 50),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9)
            };

            sharedGroupBox.Controls.Add(_sharedRadio);
            sharedGroupBox.Controls.Add(sharedDescLabel);
            Controls.Add(sharedGroupBox);

            // Continue Button
            _continueButton = new Button
            {
                Text = "Continue",
                Location = new Point(320, 280),
                Size = new Size(100, 40),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat
            };
            _continueButton.Click += ContinueButton_Click;
            Controls.Add(_continueButton);
        }

        private void IndividualRadio_CheckedChanged(object? sender, EventArgs e)
        {
            _privateRadio.Enabled = _individualRadio.Checked;
            _publicRadio.Enabled = _individualRadio.Checked;
        }

        private void SharedRadio_CheckedChanged(object? sender, EventArgs e)
        {
            _privateRadio.Enabled = _individualRadio.Checked;
            _publicRadio.Enabled = _individualRadio.Checked;
        }

        private void ContinueButton_Click(object? sender, EventArgs e)
        {
            string boardType = _individualRadio.Checked ? "individual" : "shared";
            bool isPublic = _publicRadio.Checked;

            BoardTypeSelected?.Invoke(this, new BoardTypeEventArgs
            {
                BoardType = boardType,
                IsPublic = isPublic
            });

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public class BoardTypeEventArgs : EventArgs
    {
        public string BoardType { get; set; } = "individual";
        public bool IsPublic { get; set; } = false;
    }
}
