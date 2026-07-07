using System;
using System.Windows.Forms;
using Imageboard_illust.Database;
using Imageboard_illust.Models;

namespace Imageboard_illust
{
    /// <summary>
    /// Login/Registration form for user authentication
    /// </summary>
    public class LoginForm : Form
    {
        public event EventHandler<UserLoginEventArgs>? UserLoggedIn;

        private readonly AppDatabase _db;
        private TabControl _tabControl = null!;
        private TabPage _loginTab = null!;
        private TabPage _registerTab = null!;

        // Login controls
        private TextBox _loginEmailBox = null!;
        private TextBox _loginPasswordBox = null!;
        private Button _loginButton = null!;
        private Label _loginErrorLabel = null!;

        // Register controls
        private TextBox _registerEmailBox = null!;
        private TextBox _registerPasswordBox = null!;
        private TextBox _registerConfirmBox = null!;
        private Button _registerButton = null!;
        private Label _registerErrorLabel = null!;

        public LoginForm(AppDatabase db)
        {
            _db = db;
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text = "Image Board Studio - Login";
            Size = new Size(400, 350);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = System.Drawing.Color.FromArgb(22, 22, 26);
            ForeColor = System.Drawing.Color.White;
            Font = new System.Drawing.Font("Segoe UI", 10);

            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(22, 22, 26),
                ForeColor = System.Drawing.Color.White,
            };

            // Login Tab
            _loginTab = new TabPage("Login");
            BuildLoginTab();
            _tabControl.TabPages.Add(_loginTab);

            // Register Tab
            _registerTab = new TabPage("Register");
            BuildRegisterTab();
            _tabControl.TabPages.Add(_registerTab);

            Controls.Add(_tabControl);
        }

        private void BuildLoginTab()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.FromArgb(22, 22, 26), Padding = new Padding(20) };

            var emailLabel = new Label { Text = "Email:", ForeColor = System.Drawing.Color.White, AutoSize = true, Location = new System.Drawing.Point(0, 20) };
            _loginEmailBox = new TextBox { Width = 350, Location = new System.Drawing.Point(0, 45), BackColor = System.Drawing.Color.FromArgb(40, 40, 44), ForeColor = System.Drawing.Color.White };

            var passwordLabel = new Label { Text = "Password:", ForeColor = System.Drawing.Color.White, AutoSize = true, Location = new System.Drawing.Point(0, 80) };
            _loginPasswordBox = new TextBox { Width = 350, Location = new System.Drawing.Point(0, 105), UseSystemPasswordChar = true, BackColor = System.Drawing.Color.FromArgb(40, 40, 44), ForeColor = System.Drawing.Color.White };

            _loginButton = new Button
            {
                Text = "Login",
                Width = 350,
                Height = 40,
                Location = new System.Drawing.Point(0, 150),
                BackColor = System.Drawing.Color.FromArgb(0, 120, 215),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _loginButton.Click += LoginButton_Click;

            // Forgot password link
            var forgotLink = new LinkLabel
            {
                Text = "Forgot Password?",
                AutoSize = true,
                Location = new System.Drawing.Point(0, 200),
                LinkColor = System.Drawing.Color.FromArgb(100, 200, 255)
            };
            forgotLink.LinkClicked += (_, _) => ShowForgotPasswordForm();

            _loginErrorLabel = new Label { AutoSize = true, ForeColor = System.Drawing.Color.FromArgb(255, 100, 100), Location = new System.Drawing.Point(0, 225) };

            // Divider
            var divider1 = new Label { Text = "OR", AutoSize = true, ForeColor = System.Drawing.Color.FromArgb(100, 100, 110), Location = new System.Drawing.Point(150, 260) };

            // Social login buttons
            var googleBtn = new Button
            {
                Text = "🔵 Google",
                Width = 100,
                Height = 35,
                Location = new System.Drawing.Point(0, 290),
                BackColor = System.Drawing.Color.FromArgb(66, 133, 244),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new System.Drawing.Font("Segoe UI", 9)
            };
            googleBtn.Click += (_, _) => ShowPlaceholder("Google Sign-In", "OAuth backend integration coming soon!");

            var facebookBtn = new Button
            {
                Text = "f Facebook",
                Width = 100,
                Height = 35,
                Location = new System.Drawing.Point(110, 290),
                BackColor = System.Drawing.Color.FromArgb(59, 89, 152),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new System.Drawing.Font("Segoe UI", 9)
            };
            facebookBtn.Click += (_, _) => ShowPlaceholder("Facebook Sign-In", "OAuth backend integration coming soon!");

            var appleBtn = new Button
            {
                Text = "🍎 Apple",
                Width = 100,
                Height = 35,
                Location = new System.Drawing.Point(220, 290),
                BackColor = System.Drawing.Color.FromArgb(0, 0, 0),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new System.Drawing.Font("Segoe UI", 9)
            };
            appleBtn.Click += (_, _) => ShowPlaceholder("Apple Sign-In", "OAuth backend integration coming soon!");

            panel.Controls.Add(emailLabel);
            panel.Controls.Add(_loginEmailBox);
            panel.Controls.Add(passwordLabel);
            panel.Controls.Add(_loginPasswordBox);
            panel.Controls.Add(_loginButton);
            panel.Controls.Add(forgotLink);
            panel.Controls.Add(_loginErrorLabel);
            panel.Controls.Add(divider1);
            panel.Controls.Add(googleBtn);
            panel.Controls.Add(facebookBtn);
            panel.Controls.Add(appleBtn);

            _loginTab.Controls.Add(panel);
        }

        private void ShowForgotPasswordForm()
        {
            var forgotForm = new ForgotPasswordForm(_db);
            forgotForm.PasswordResetComplete += (_, _) =>
            {
                _loginErrorLabel.ForeColor = System.Drawing.Color.FromArgb(100, 255, 100);
                _loginErrorLabel.Text = "Password reset successfully! Please login with your new password.";
            };
            forgotForm.ShowDialog(this);
        }

        private void ShowPlaceholder(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BuildRegisterTab()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.FromArgb(22, 22, 26), Padding = new Padding(20) };

            var emailLabel = new Label { Text = "Email:", ForeColor = System.Drawing.Color.White, AutoSize = true, Location = new System.Drawing.Point(0, 20) };
            _registerEmailBox = new TextBox { Width = 350, Location = new System.Drawing.Point(0, 45), BackColor = System.Drawing.Color.FromArgb(40, 40, 44), ForeColor = System.Drawing.Color.White };

            var passwordLabel = new Label { Text = "Password:", ForeColor = System.Drawing.Color.White, AutoSize = true, Location = new System.Drawing.Point(0, 80) };
            _registerPasswordBox = new TextBox { Width = 350, Location = new System.Drawing.Point(0, 105), UseSystemPasswordChar = true, BackColor = System.Drawing.Color.FromArgb(40, 40, 44), ForeColor = System.Drawing.Color.White };

            var confirmLabel = new Label { Text = "Confirm Password:", ForeColor = System.Drawing.Color.White, AutoSize = true, Location = new System.Drawing.Point(0, 140) };
            _registerConfirmBox = new TextBox { Width = 350, Location = new System.Drawing.Point(0, 165), UseSystemPasswordChar = true, BackColor = System.Drawing.Color.FromArgb(40, 40, 44), ForeColor = System.Drawing.Color.White };

            _registerButton = new Button
            {
                Text = "Create Account",
                Width = 350,
                Height = 40,
                Location = new System.Drawing.Point(0, 210),
                BackColor = System.Drawing.Color.FromArgb(0, 120, 215),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _registerButton.Click += RegisterButton_Click;

            _registerErrorLabel = new Label { AutoSize = true, ForeColor = System.Drawing.Color.FromArgb(255, 100, 100), Location = new System.Drawing.Point(0, 260) };

            panel.Controls.Add(emailLabel);
            panel.Controls.Add(_registerEmailBox);
            panel.Controls.Add(passwordLabel);
            panel.Controls.Add(_registerPasswordBox);
            panel.Controls.Add(confirmLabel);
            panel.Controls.Add(_registerConfirmBox);
            panel.Controls.Add(_registerButton);
            panel.Controls.Add(_registerErrorLabel);

            _registerTab.Controls.Add(panel);
        }

        private void LoginButton_Click(object? sender, EventArgs e)
        {
            string email = _loginEmailBox.Text.Trim();
            string password = _loginPasswordBox.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                _loginErrorLabel.Text = "Email and password required";
                return;
            }

            User? user = _db.LoginUser(email, password);
            if (user == null)
            {
                _loginErrorLabel.Text = "Invalid email or password";
                return;
            }

            UserLoggedIn?.Invoke(this, new UserLoginEventArgs { User = user });
            Close();
        }

        private void RegisterButton_Click(object? sender, EventArgs e)
        {
            string email = _registerEmailBox.Text.Trim();
            string password = _registerPasswordBox.Text;
            string confirm = _registerConfirmBox.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                _registerErrorLabel.Text = "Email and password required";
                return;
            }

            if (password != confirm)
            {
                _registerErrorLabel.Text = "Passwords do not match";
                return;
            }

            if (password.Length < 6)
            {
                _registerErrorLabel.Text = "Password must be at least 6 characters";
                return;
            }

            User? user = _db.RegisterUser(email, password);
            if (user == null)
            {
                _registerErrorLabel.Text = "Email already registered";
                return;
            }

            _registerErrorLabel.ForeColor = System.Drawing.Color.FromArgb(100, 255, 100);
            _registerErrorLabel.Text = "Account created! Switching to login...";

            System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ =>
            {
                Invoke(new Action(() =>
                {
                    _tabControl.SelectedTab = _loginTab;
                    _loginEmailBox.Text = email;
                    _loginPasswordBox.Text = "";
                    _loginErrorLabel.Text = "";
                    _registerErrorLabel.ForeColor = System.Drawing.Color.FromArgb(255, 100, 100);
                    _registerErrorLabel.Text = "";
                    _registerEmailBox.Clear();
                    _registerPasswordBox.Clear();
                    _registerConfirmBox.Clear();
                }));
            });
        }
    }

    public class UserLoginEventArgs : EventArgs
    {
        public User? User { get; set; }
    }
}
