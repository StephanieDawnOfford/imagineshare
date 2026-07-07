using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Imageboard_illust.Database;

namespace Imageboard_illust
{
    public partial class ShareBoardDialog : Form
    {
        private readonly AppDatabase _db;
        private readonly int _boardId;
        private readonly int _currentUserId;
        private string _shareCode = "";

        public ShareBoardDialog(AppDatabase db, int boardId, int currentUserId)
        {
            _db = db;
            _boardId = boardId;
            _currentUserId = currentUserId;
            InitializeComponent();
            LoadShareCode();
            LoadMembers();
        }

        private void InitializeComponent()
        {
            this.Text = "Manage Shared Board";
            this.Width = 500;
            this.Height = 450;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Share Code Section
            var lblShareCode = new Label
            {
                Text = "Share Code (copy and send to collaborators):",
                Left = 20,
                Top = 20,
                Width = 450,
                AutoSize = true
            };
            this.Controls.Add(lblShareCode);

            var txtShareCode = new TextBox
            {
                Name = "txtShareCode",
                Left = 20,
                Top = 45,
                Width = 350,
                Height = 30,
                ReadOnly = true,
                Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold)
            };
            this.Controls.Add(txtShareCode);

            var btnCopy = new Button
            {
                Text = "Copy",
                Left = 380,
                Top = 45,
                Width = 80,
                Height = 30
            };
            btnCopy.Click += (s, e) =>
            {
                Clipboard.SetText(txtShareCode.Text);
                MessageBox.Show("Share code copied to clipboard!", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            this.Controls.Add(btnCopy);

            // Members Section
            var lblMembers = new Label
            {
                Text = "Board Members:",
                Left = 20,
                Top = 90,
                Width = 450,
                AutoSize = true,
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold)
            };
            this.Controls.Add(lblMembers);

            // DataGridView for members
            var dgvMembers = new DataGridView
            {
                Name = "dgvMembers",
                Left = 20,
                Top = 115,
                Width = 440,
                Height = 200,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false
            };

            // Add columns
            dgvMembers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Email", Name = "Email", ReadOnly = true });
            dgvMembers.Columns.Add(new DataGridViewComboBoxColumn
            {
                HeaderText = "Permission",
                Name = "Permission",
                Items = { "blocked", "view", "edit" }
            });
            dgvMembers.Columns.Add(new DataGridViewButtonColumn
            {
                HeaderText = "Remove",
                Name = "Remove",
                Text = "Remove",
                UseColumnTextForButtonValue = true
            });

            dgvMembers.CellContentClick += (s, e) =>
            {
                if (e.ColumnIndex == 2) // Remove button
                {
                    int userId = (int)dgvMembers.Rows[e.RowIndex].Tag;
                    if (MessageBox.Show("Remove this member?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        _db.RemoveBoardMember(_boardId, userId);
                        LoadMembers();
                    }
                }
            };

            dgvMembers.CellValueChanged += (s, e) =>
            {
                if (e.ColumnIndex == 1) // Permission column
                {
                    int userId = (int)dgvMembers.Rows[e.RowIndex].Tag;
                    string permission = dgvMembers.Rows[e.RowIndex].Cells[1].Value?.ToString() ?? "view";
                    _db.UpdateMemberPermission(_boardId, userId, permission);
                }
            };

            this.Controls.Add(dgvMembers);

            // Close Button
            var btnClose = new Button
            {
                Text = "Close",
                Left = 200,
                Top = 330,
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.OK
            };
            this.Controls.Add(btnClose);
            this.AcceptButton = btnClose;
        }

        private void LoadShareCode()
        {
            _shareCode = _db.GetBoardShareCode(_boardId);
            if (string.IsNullOrEmpty(_shareCode))
            {
                _shareCode = _db.CreateShareCode(_boardId);
            }
            this.Controls["txtShareCode"].Text = _shareCode;
        }

        private void LoadMembers()
        {
            var dgvMembers = this.Controls["dgvMembers"] as DataGridView;
            if (dgvMembers == null) return;

            dgvMembers.Rows.Clear();

            var members = _db.GetBoardMembers(_boardId);
            foreach (var (userId, email, permission) in members)
            {
                int rowIndex = dgvMembers.Rows.Add(email, permission ?? "view");
                dgvMembers.Rows[rowIndex].Tag = userId;
            }
        }
    }
}
