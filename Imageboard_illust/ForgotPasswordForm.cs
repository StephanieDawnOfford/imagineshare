using System;
using System.Windows.Forms;
using System.Drawing;
using Imageboard_illust.Database;

namespace Imageboard_illust
{
    /// <summary>
    /// Forgot Password form with two-step reset process
    /// Step 1: Request reset token via email
    /// Step 2: Reset password with token
    /// </summary>
    public class ForgotPasswordForm : Form
    {
        public event EventHandler? PasswordResetComplete;

        private readonly AppDatabase _db;
        private Panel _stepPanel = null!;
        private Label _stepLabel = null!;
        private TextBox _emailBox = null!;
        private TextBox _tokenBox = null!;
        private TextBox _passwordBox = null!;
        private TextBox _confirmBox = null!;
        private Button _actionButton = null!;
        private Label _messageLabel = null!;
        private Button _backButton = null!;
        private int _currentStep = 1; // Step 1: Email request, Step 2: Token + Password

        public ForgotPasswordForm(AppDatabase db)
        {
            _db = db;
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text = "Forgot Password";
            Size = new Size(450, 450);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(22, 22, 26);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10);
            MaximizeBox = false;
            MinimizeBox = false;

            // Step indicator
            _stepLabel = new Label
            {
                Text = "Step 1: Enter your email",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 20)
            };
            Controls.Add(_stepLabel);

            // Panel for form controls
            _stepPanel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(410, 280),
                BackColor = Color.Transparent
            };
            Controls.Add(_stepPanel);

            // Step 1: Email request
            var emailLabel = new Label
            {
                Text = "Email address:",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            _emailBox = new TextBox
            {
                Width = 350,
                Location = new Point(0, 25),
                BackColor = Color.FromArgb(40, 40, 44),
                ForeColor = Color.White
            };

            var infoLabel = new Label
            {
                Text = "We'll send you a reset code to this email address.",
                ForeColor = Color.FromArgb(150, 150, 160),
                Font = new Font("Segoe UI", 9),
                AutoSize = true,
                Location = new Point(0, 55)
            };

            _stepPanel.Controls.Add(emailLabel);
            _stepPanel.Controls.Add(_emailBox);
            _stepPanel.Controls.Add(infoLabel);

            // Step 2: Token and password (hidden initially)
            var tokenLabel = new Label
            {
                Text = "Reset Code (check your email):",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(0, 0),
                Visible = false
            };

            _tokenBox = new TextBox
            {
                Width = 350,
                Location = new Point(0, 25),
                BackColor = Color.FromArgb(40, 40, 44),
                ForeColor = Color.White,
                Visible = false
            };

            var passwordLabel = new Label
            {
                Text = "New Password:",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(0, 60),
                Visible = false
            };

            _passwordBox = new TextBox
            {
                Width = 350,
                Location = new Point(0, 85),
                UseSystemPasswordChar = true,
                BackColor = Color.FromArgb(40, 40, 44),
                ForeColor = Color.White,
                Visible = false
            };

            var confirmLabel = new Label
            {
                Text = "Confirm Password:",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(0, 120),
                Visible = false
            };

            _confirmBox = new TextBox
            {
                Width = 350,
                Location = new Point(0, 145),
                UseSystemPasswordChar = true,
                BackColor = Color.FromArgb(40, 40, 44),
                ForeColor = Color.White,
                Visible = false
            };

            _stepPanel.Controls.Add(tokenLabel);
            _stepPanel.Controls.Add(_tokenBox);
            _stepPanel.Controls.Add(passwordLabel);
            _stepPanel.Controls.Add(_passwordBox);
            _stepPanel.Controls.Add(confirmLabel);
            _stepPanel.Controls.Add(_confirmBox);

            // Message label
            _messageLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 100, 100),
                Location = new Point(0, 200)
            };
            _stepPanel.Controls.Add(_messageLabel);

            // Back button
            _backButton = new Button
            {
                Text = "Back",
                Size = new Size(100, 40),
                Location = new Point(20, 360),
                BackColor = Color.FromArgb(80, 80, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _backButton.Click += (_, _) => Close();
            Controls.Add(_backButton);

            // Action button
            _actionButton = new Button
            {
                Text = "Send Reset Code",
                Size = new Size(200, 40),
                Location = new Point(250, 360),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _actionButton.Click += ActionButton_Click;
            Controls.Add(_actionButton);
        }

        private void ActionButton_Click(object? sender, EventArgs e)
        {
            if (_currentStep == 1)
                RequestReset();
            else
                CompleteReset();
        }

        private void RequestReset()
        {
            string email = _emailBox.Text.Trim();
            if (string.IsNullOrEmpty(email))
            {
                ShowError("Please enter your email address");
                return;
            }

            string resetToken = _db.RequestPasswordReset(email);
            if (resetToken == null)
            {
                ShowError("Email not found");
                return;
            }

            // Show reset token to user (in real app, this would be emailed)
            ShowMessage($"Reset code: {resetToken}", Color.FromArgb(100, 255, 100));

            // Switch to step 2
            _currentStep = 2;
            _stepLabel.Text = "Step 2: Enter reset code and new password";
            _actionButton.Text = "Reset Password";

            // Hide step 1 controls
            _emailBox.Visible = false;
            ((Label)_stepPanel.Controls[0]).Visible = false;
            ((Label)_stepPanel.Controls[2]).Visible = false;

            // Show step 2 controls
            _stepPanel.Controls[1].Visible = true; // token label
            _tokenBox.Visible = true;
            _stepPanel.Controls[3].Visible = true; // password label
            _passwordBox.Visible = true;
            _stepPanel.Controls[4].Visible = true; // confirm label
            _confirmBox.Visible = true;

            _tokenBox.Text = resetToken; // Pre-fill token for testing
        }

        private void CompleteReset()
        {
            string token = _tokenBox.Text.Trim();
            string password = _passwordBox.Text;
            string confirm = _confirmBox.Text;

            if (string.IsNullOrEmpty(token))
            {
                ShowError("Please enter your reset code");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Please enter a new password");
                return;
            }

            if (password != confirm)
            {
                ShowError("Passwords do not match");
                return;
            }

            if (password.Length < 6)
            {
                ShowError("Password must be at least 6 characters");
                return;
            }

            if (_db.ResetPassword(token, password))
            {
                ShowMessage("Password reset successfully!", Color.FromArgb(100, 255, 100));
                System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ =>
                {
                    Invoke(new Action(() =>
                    {
                        PasswordResetComplete?.Invoke(this, EventArgs.Empty);
                        Close();
                    }));
                });
            }
            else
            {
                ShowError("Invalid or expired reset code");
            }
        }

        private void ShowError(string message)
        {
            _messageLabel.ForeColor = Color.FromArgb(255, 100, 100);
            _messageLabel.Text = message;
        }

        private void ShowMessage(string message, Color color)
        {
            _messageLabel.ForeColor = color;
            _messageLabel.Text = message;
        }
    }
}
