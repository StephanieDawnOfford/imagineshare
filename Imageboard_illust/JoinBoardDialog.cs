using System;
using System.Windows.Forms;
using Imageboard_illust.Database;

namespace Imageboard_illust
{
    public partial class JoinBoardDialog : Form
    {
        private readonly AppDatabase _db;
        private readonly int _currentUserId;

        public JoinBoardDialog(AppDatabase db, int currentUserId)
        {
            _db = db;
            _currentUserId = currentUserId;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Join Shared Board";
            this.Width = 400;
            this.Height = 200;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Label
            var lblInstructions = new Label
            {
                Text = "Enter the board share code (e.g., ABC123):",
                Left = 20,
                Top = 20,
                Width = 350,
                AutoSize = true
            };
            this.Controls.Add(lblInstructions);

            // TextBox for share code
            var txtShareCode = new TextBox
            {
                Name = "txtShareCode",
                Left = 20,
                Top = 50,
                Width = 350,
                Height = 30,
                Font = new System.Drawing.Font("Arial", 12)
            };
            this.Controls.Add(txtShareCode);

            // Join Button
            var btnJoin = new Button
            {
                Text = "Join Board",
                Left = 120,
                Top = 100,
                Width = 100,
                Height = 30
            };
            btnJoin.Click += (s, e) =>
            {
                string shareCode = txtShareCode.Text.Trim().ToUpper();
                if (string.IsNullOrEmpty(shareCode))
                {
                    MessageBox.Show("Please enter a share code.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (_db.JoinBoard(shareCode, _currentUserId))
                {
                    MessageBox.Show("Successfully joined the board!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Invalid share code or you're already a member.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            this.Controls.Add(btnJoin);

            // Cancel Button
            var btnCancel = new Button
            {
                Text = "Cancel",
                Left = 230,
                Top = 100,
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(btnCancel);
            this.CancelButton = btnCancel;
        }
    }
}
