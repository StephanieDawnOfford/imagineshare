using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Imageboard_illust.Database;
using Imageboard_illust.Models;

namespace Imageboard_illust
{
    public class MainForm : Form
    {
        private readonly AppDatabase _db = new();
        private User? _currentUser = null;
        private StartPage? _startPage;
        private ImageBoardPage? _boardPage;
        private ColorPickerPage? _colorPage;

        public MainForm()
        {
            Text = "Image Board Studio";
            Size = new Size(1440, 900);
            MinimumSize = new Size(960, 640);
            BackColor = Color.FromArgb(22, 22, 26);

            ShowIntroPage();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        { _db.Dispose(); base.OnFormClosed(e); }
        // ══════════════════════════════════════════════════════
        //  Page switching
        // ══════════════════════════════════════════════════════

        private void ShowIntroPage()
        {
            ClearPages();
            var introPage = new IntroPage();
            introPage.ContinueRequested += (_, _) => ShowLoginForm();
            Controls.Add(introPage);
            Text = "Image Board Studio";
        }

        private void ShowLoginForm()
        {
            ClearPages();
            var loginForm = new LoginForm(_db);
            loginForm.UserLoggedIn += (_, args) =>
            {
                if (args.User != null)
                {
                    _currentUser = args.User;
                    ShowStartPage();
                }
            };
            loginForm.ShowDialog(this);
        }

        private void ShowStartPage()
        {
            ClearPages();
            _startPage = new StartPage(_currentUser);
            _startPage.IndividualBoardChosen += (_, _) => CreateBoardAndShow("individual", "My Board", false);
            _startPage.SharedBoardChosen += (_, _) => CreateBoardAndShow("shared", "Shared Board", false);
            _startPage.ColorPickerChosen += (_, _) => ShowColorPicker();
            _startPage.LogoutRequested += (_, _) => 
            {
                _currentUser = null;
                ShowLoginForm();
            };
            Controls.Add(_startPage);
            Text = "Image Board Studio";
        }

        private void CreateBoardAndShow(string boardType, string boardName, bool isPublic)
        {
            if (_currentUser == null) return;
            _db.InsertBoard(_currentUser.Id, boardName, boardType, isPublic);
            ShowImageBoard();
        }

        private void ShowBoardTypeDialog()
        {
            var dialog = new BoardTypeDialog();
            dialog.BoardTypeSelected += (_, args) =>
            {
                if (_currentUser != null)
                {
                    // Create a new board with the selected type
                    string boardName = args.BoardType == "shared" ? "Shared Board" : "My Board";
                    int boardId = _db.InsertBoard(_currentUser.Id, boardName, args.BoardType, args.IsPublic);
                    ShowImageBoard();
                }
            };
            dialog.ShowDialog(this);
        }

        private void ShowImageBoard()
        {
            ClearPages();
            if (_currentUser != null)
            {
                _boardPage = new ImageBoardPage(_db, _currentUser);
                _boardPage.HomeRequested += (_, _) => ShowStartPage();
                Controls.Add(_boardPage);
                Text = "Image Board Studio  —  Image Board";
            }
        }

        private void ShowColorPicker()
        {
            ClearPages();
            _colorPage = new ColorPickerPage(_db);
            _colorPage.BackRequested += (_, _) => ShowStartPage();
            Controls.Add(_colorPage);
            Text = "Image Board Studio  —  Color Picker";
        }

        private void ClearPages()
        {
            foreach (Control c in Controls) c.Dispose();
            Controls.Clear();
            _startPage = null;
            _boardPage = null;
            _colorPage = null;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Image Board Page
    // ══════════════════════════════════════════════════════════
    public class ImageBoardPage : Panel
    {
        public event EventHandler? HomeRequested;

        private Panel _toolbar = null!;
        private SplitContainer _sidebar = null!;
        private ListView _boardList = null!;
        private ListView _imageList = null!;
        private Panel _canvas = null!;
        private NumericUpDown _numZoom = null!;
        private StatusStrip _status = null!;
        private ToolStripStatusLabel _statusLbl = null!;
        private ToolStripStatusLabel _tipLbl = null!;

        private readonly AppDatabase _db;
        private readonly User? _currentUser;
        private Board? _currentBoard = null;
        private int _nextZOrder = 0;
        private int _placeStep = 0;
        private readonly Dictionary<int, ImageItemControl> _cards = new();

        private float _zoom = 1.0f;
        private bool _suppressNumEvent = false;
        private const float ZoomStep = 0.1f;
        private const float ZoomMin = 0.5f;
        private const float ZoomMax = 2.0f;

        private const int WM_MOUSEHWHEEL = 0x020E;

        private static readonly string[] ValidExts =
            { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tiff", ".tif" };

        public ImageBoardPage(AppDatabase db, User? user = null)
        {
            _db = db;
            _currentUser = user;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(30, 30, 32);

            BuildToolbar();
            BuildSidebarSplit();
            BuildCanvas();
            BuildStatusBar();

            Controls.Add(_canvas);
            Controls.Add(_sidebar);
            Controls.Add(_status);
            Controls.Add(_toolbar);

            SetupKeyboard();
            RefreshBoardList();
        }

        // ── Forward WM_MOUSEHWHEEL to parent Form ────────────────────
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEHWHEEL)
            {
                int delta = (int)m.WParam >> 16;
                int newX = Math.Max(0, -_canvas.AutoScrollPosition.X + (delta > 0 ? 60 : -60));
                _canvas.AutoScrollPosition = new Point(newX, -_canvas.AutoScrollPosition.Y);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        // ══════════════════════════════════════════════════════
        //  Toolbar
        // ══════════════════════════════════════════════════════
        private void BuildToolbar()
        {
            _toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = Color.FromArgb(37, 37, 38),
            };

            // Right panel (zoom controls)
            var rightPanel = new Panel
            { Dock = DockStyle.Right, Width = 230, BackColor = Color.Transparent };

            var yellowMinus = ZoomIconBtn("－");
            var yellowPlus = ZoomIconBtn("＋");

            _numZoom = new NumericUpDown
            {
                Minimum = (decimal)(ZoomMin * 100),
                Maximum = (decimal)(ZoomMax * 100),
                Value = 100,
                Increment = 10,
                Width = 60,
                Height = 28,
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.FromArgb(52, 52, 48),
                ForeColor = Color.FromArgb(255, 240, 180),
                Font = new Font("Consolas", 10f, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle,
                DecimalPlaces = 0,
            };
            _numZoom.ValueChanged += (_, _) =>
            { if (!_suppressNumEvent) SetZoom((float)_numZoom.Value / 100f, true); };

            var lblPct = new Label
            {
                Text = "%",
                ForeColor = Color.FromArgb(200, 185, 120),
                Font = new Font("Segoe UI", 9f),
                Width = 16,
                Height = 36,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            var btnReset = new Button
            {
                Text = "",
                Width = 52,
                Height = 36,
                BackColor = Color.FromArgb(58, 56, 40),
                ForeColor = Color.FromArgb(240, 220, 140),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f),
                Cursor = Cursors.Hand,
            };
            btnReset.FlatAppearance.BorderColor = Color.FromArgb(120, 110, 60);
            btnReset.FlatAppearance.BorderSize = 1;
            btnReset.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 76, 50);
            btnReset.Paint += (s, e) =>
            {
                var rect = ((Button)s!).ClientRectangle;
                using var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
                using var font = new Font("Segoe UI", 8f, FontStyle.Bold);
                using var brush = new SolidBrush(Color.FromArgb(240, 220, 140));
                e.Graphics.DrawString("100%", font, brush, rect, sf);
            };
            btnReset.Click += (_, _) => SetZoom(1.0f);
            yellowMinus.Click += (_, _) => AdjustZoom(-ZoomStep);
            yellowPlus.Click += (_, _) => AdjustZoom(+ZoomStep);
            yellowMinus.Paint += MakeZoomBtnPaint("－");
            yellowPlus.Paint += MakeZoomBtnPaint("＋");

            int rx = 8;
            btnReset.Location = new Point(rx, 9); rx += btnReset.Width + 8;
            yellowMinus.Location = new Point(rx, 9); rx += yellowMinus.Width + 4;
            _numZoom.Location = new Point(rx, 13); rx += _numZoom.Width;
            lblPct.Location = new Point(rx, 9); rx += lblPct.Width + 4;
            yellowPlus.Location = new Point(rx, 9);
            rightPanel.Controls.AddRange(
                new Control[] { btnReset, yellowMinus, _numZoom, lblPct, yellowPlus });

            // Left panel (action buttons)
            var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            var btnHome = LeftBtn("🏠 Home", Color.FromArgb(55, 55, 65), 90);
            var btnNew = LeftBtn("＋ New Board", Color.FromArgb(55, 120, 50), 118);
            var btnOpen = LeftBtn("📂 Open Images", Color.FromArgb(0, 122, 204), 120);
            var btnPaste = LeftBtn("📋 Paste  Ctrl+V", Color.FromArgb(0, 150, 136), 152);
            var btnExport = LeftBtn("📤 Export PNG", Color.FromArgb(160, 100, 0), 104);
            var btnDelete = LeftBtn("🗑 Delete Board", Color.FromArgb(155, 35, 25), 120);

            int lx = 12;
            foreach (var b in new[] { btnHome, btnNew, btnOpen, btnPaste, btnExport, btnDelete })
            { b.Location = new Point(lx, 9); lx += b.Width + 6; }

            btnHome.Click += (_, _) => HomeRequested?.Invoke(this, EventArgs.Empty);
            btnNew.Click += (_, _) => CreateNewBoard();
            btnOpen.Click += (_, _) => OpenFileDialogUI();
            btnPaste.Click += (_, _) => PasteFromClipboard();
            btnExport.Click += (_, _) => ExportBoardAsPng();
            btnDelete.Click += (_, _) => DeleteCurrentBoard();

            leftPanel.Controls.AddRange(
                new Control[] { btnHome, btnNew, btnOpen, btnPaste, btnExport, btnDelete });

            _toolbar.Controls.Add(rightPanel);
            _toolbar.Controls.Add(leftPanel);
        }

        private static PaintEventHandler MakeZoomBtnPaint(string text) => (s, e) =>
        {
            var btn = (Button)s!;
            var rect = btn.ClientRectangle;
            using var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
            using var font = new Font("Segoe UI", 14f, FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb(255, 235, 120));
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            e.Graphics.DrawString(text, font, brush, rect, sf);
        };

        private static Button LeftBtn(string text, Color bg, int w)
        {
            var b = new Button
            {
                Text = text,
                Width = w,
                Height = 36,
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.2f);
            return b;
        }

        private static Button ZoomIconBtn(string text)
        {
            var bg = Color.FromArgb(65, 62, 38);
            var b = new Button
            {
                Text = "",
                Width = 36,
                Height = 36,
                BackColor = bg,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(120, 110, 55);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 85, 50);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(45, 43, 28);
            return b;
        }

        // ══════════════════════════════════════════════════════
        //  Zoom
        // ══════════════════════════════════════════════════════
        private void AdjustZoom(float d) => SetZoom(_zoom + d);

        private void SetZoom(float v, bool fromNum = false)
        {
            float c2 = Math.Clamp((float)(Math.Round(v * 10) / 10), ZoomMin, ZoomMax);
            if (Math.Abs(c2 - _zoom) < 0.001f) return;
            _zoom = c2;
            ApplyZoomToAllCards();
            if (!fromNum)
            {
                _suppressNumEvent = true;
                _numZoom.Value = (decimal)Math.Round(_zoom * 100);
                _suppressNumEvent = false;
            }
            UpdateStatus();
        }

        private void ApplyZoomToAllCards()
        {
            foreach (var card in _cards.Values)
            {
                card.Location = new Point((int)(card.Item.X * _zoom), (int)(card.Item.Y * _zoom));
                card.Size = new Size((int)(card.Item.Width * _zoom), (int)(card.Item.Height * _zoom));
            }
        }

        // ══════════════════════════════════════════════════════
        //  Keyboard shortcuts
        // ══════════════════════════════════════════════════════
        private void SetupKeyboard()
        {
            var form = FindForm();
            if (form != null) { form.KeyPreview = true; form.KeyDown += Form_KeyDown; }
            VisibleChanged += (_, _) =>
            {
                var f = FindForm();
                if (f != null) { f.KeyDown -= Form_KeyDown; if (Visible) f.KeyDown += Form_KeyDown; }
            };
        }

        private void Form_KeyDown(object? s, KeyEventArgs e)
        {
            if (!e.Control) return;
            switch (e.KeyCode)
            {
                case Keys.V: PasteFromClipboard(); e.Handled = true; break;
                case Keys.Oemplus: case Keys.Add: AdjustZoom(+ZoomStep); e.Handled = true; break;
                case Keys.OemMinus: case Keys.Subtract: AdjustZoom(-ZoomStep); e.Handled = true; break;
                case Keys.D0: case Keys.NumPad0: SetZoom(1.0f); e.Handled = true; break;
            }
        }

        // ══════════════════════════════════════════════════════
        //  Sidebar
        // ══════════════════════════════════════════════════════
        private void BuildSidebarSplit()
        {
            _sidebar = new SplitContainer
            {
                Dock = DockStyle.Left,
                Width = 220,
                Orientation = Orientation.Horizontal,
                BackColor = Color.FromArgb(30, 30, 32),
                SplitterDistance = 280,
                Panel1MinSize = 120,
                Panel2MinSize = 80,
                BorderStyle = BorderStyle.None,
                SplitterWidth = 4,
            };

            // Board list (top pane)
            var boardHeader = SideHeader("📁  Boards");
            
            var btnJoinSide = new Button
            {
                Text = "👥",
                Width = 26,
                Height = 22,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 100, 120),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
            };
            btnJoinSide.FlatAppearance.BorderSize = 0;
            btnJoinSide.Click += (_, _) => JoinBoard();
            boardHeader.Controls.Add(btnJoinSide);
            
            var btnNewSide = new Button
            {
                Text = "＋",
                Width = 26,
                Height = 22,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 120, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
            };
            btnNewSide.FlatAppearance.BorderSize = 0;
            btnNewSide.Click += (_, _) => CreateNewBoard();
            boardHeader.Controls.Add(btnNewSide);

            _boardList = SideList();
            _boardList.Columns.Add("ID", 44);
            _boardList.Columns.Add("Board Name", 148);
            _boardList.SelectedIndexChanged += (_, _) =>
            {
                if (_boardList.SelectedItems.Count == 0) return;
                int id = (int)_boardList.SelectedItems[0].Tag!;
                if (_currentBoard?.Id == id) return;
                LoadBoard(id);
            };
            _boardList.DoubleClick += (_, _) =>
            {
                if (_boardList.SelectedItems.Count > 0)
                    LoadBoard((int)_boardList.SelectedItems[0].Tag!);
            };

            var boardCtx = new ContextMenuStrip();
            boardCtx.Items.Add("Select this board", null, (_, _) => { if (_boardList.SelectedItems.Count > 0) LoadBoard((int)_boardList.SelectedItems[0].Tag!); });
            boardCtx.Items.Add("Rename board…", null, (_, _) => BoardList_Rename());
            boardCtx.Items.Add("Share board…", null, (_, _) => ShareBoard());
            boardCtx.Items.Add(new ToolStripSeparator());
            boardCtx.Items.Add("Delete this board", null, (_, _) => DeleteCurrentBoard());
            _boardList.ContextMenuStrip = boardCtx;

            _sidebar.Panel1.Controls.Add(_boardList);
            _sidebar.Panel1.Controls.Add(boardHeader);

            // Image list (bottom pane)
            var imgHeader = SideHeader("🖼  Images");
            _imageList = SideList();
            _imageList.Columns.Add("ID", 44);
            _imageList.Columns.Add("Name", 148);
            _imageList.DoubleClick += (_, _) => ImageList_Jump();

            var imgCtx = new ContextMenuStrip();
            imgCtx.Items.Add("Jump to card", null, (_, _) => ImageList_Jump());
            imgCtx.Items.Add("Rename…", null, (_, _) => ImageList_Rename());
            imgCtx.Items.Add(new ToolStripSeparator());
            imgCtx.Items.Add("Delete", null, (_, _) => ImageList_Delete());
            _imageList.ContextMenuStrip = imgCtx;

            _sidebar.Panel2.Controls.Add(_imageList);
            _sidebar.Panel2.Controls.Add(imgHeader);
        }

        private static Label SideHeader(string t) => new()
        {
            Text = t,
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Color.FromArgb(28, 28, 30),
            ForeColor = Color.FromArgb(160, 160, 165),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
        };

        private static ListView SideList() => new()
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            HeaderStyle = ColumnHeaderStyle.None,
            BackColor = Color.FromArgb(42, 42, 45),
            ForeColor = Color.FromArgb(200, 200, 200),
            Font = new Font("Segoe UI", 8.5f),
            BorderStyle = BorderStyle.None,
            MultiSelect = false,
        };

        // ══════════════════════════════════════════════════════
        //  Canvas
        // ══════════════════════════════════════════════════════
        private void BuildCanvas()
        {
            _canvas = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMinSize = new Size(4000, 3000),
                BackColor = Color.FromArgb(40, 40, 43),
                AllowDrop = true,
            };
            _canvas.Paint += (_, e) =>
            {
                const int G = 40;
                using var p = new Pen(Color.FromArgb(50, 50, 53), 1);
                int ox = _canvas.AutoScrollPosition.X % G, oy = _canvas.AutoScrollPosition.Y % G;
                for (int x = ox; x < _canvas.Width; x += G) e.Graphics.DrawLine(p, x, 0, x, _canvas.Height);
                for (int y = oy; y < _canvas.Height; y += G) e.Graphics.DrawLine(p, 0, y, _canvas.Width, y);
            };
            _canvas.DragEnter += (_, e) =>
                e.Effect = (e.Data?.GetDataPresent(DataFormats.FileDrop) == true ||
                            e.Data?.GetDataPresent(DataFormats.Bitmap) == true)
                    ? DragDropEffects.Copy : DragDropEffects.None;
            _canvas.DragDrop += Canvas_DragDrop;
            _canvas.MouseWheel += (_, e) =>
            {
                if (ModifierKeys.HasFlag(Keys.Control))
                    AdjustZoom(e.Delta > 0 ? +ZoomStep : -ZoomStep);
                else
                {
                    int ny = Math.Max(0, -_canvas.AutoScrollPosition.Y + (e.Delta > 0 ? -80 : 80));
                    _canvas.AutoScrollPosition = new Point(-_canvas.AutoScrollPosition.X, ny);
                }
            };
        }

        private void BuildStatusBar()
        {
            _status = new StatusStrip { BackColor = Color.FromArgb(37, 37, 38) };
            _statusLbl = new ToolStripStatusLabel { ForeColor = Color.FromArgb(200, 200, 200), Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _tipLbl = new ToolStripStatusLabel { ForeColor = Color.FromArgb(220, 160, 40) };
            _status.Items.Add(_statusLbl);
            _status.Items.Add(_tipLbl);
        }

        private void Canvas_DragDrop(object? s, DragEventArgs e)
        {
            if (!EnsureBoard()) return;
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null) foreach (var f in files) AddFromFile(f);
            }
            else if (e.Data?.GetDataPresent(DataFormats.Bitmap) == true)
            {
                var img = e.Data.GetData(DataFormats.Bitmap) as Image;
                if (img != null) AddToBoard(img, "Dropped image");
            }
        }

        // ══════════════════════════════════════════════════════
        //  Board operations
        // ══════════════════════════════════════════════════════
        private void CreateNewBoard()
        {
            string name = PromptString("New board name", "New Board");
            if (name == null!) return;
            if (name.Trim().Length == 0) name = "New Board";
            
            // Create board with current user
            int id = _currentUser != null 
                ? _db.InsertBoard(_currentUser.Id, name.Trim(), "individual", false)
                : _db.InsertBoard(_currentUser?.Id ?? 1, name.Trim());
            
            var board = new Board { Id = id, OwnerId = _currentUser?.Id ?? 1, Name = name.Trim(), BoardType = "individual" };
            Directory.CreateDirectory(board.ImagesDir(_db.DataDir));
            RefreshBoardList();
            SelectBoardById(id);
        }

        private void DeleteCurrentBoard()
        {
            if (_currentBoard == null) { Tip("No board selected"); return; }
            var r = MessageBox.Show(
                $"Delete board #{_currentBoard.Id:D3} \"{_currentBoard.Name}\" and all its images?",
                "Delete Board", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            int did = _currentBoard.Id;
            string dir = Path.Combine(_db.DataDir, "boards", did.ToString());
            ClearCanvas(); _db.DeleteBoard(did);
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            _currentBoard = null; RefreshBoardList(); UpdateStatus();
        }

        private void BoardList_Rename()
        {
            if (_boardList.SelectedItems.Count == 0) return;
            int id = (int)_boardList.SelectedItems[0].Tag!;
            string cur = _boardList.SelectedItems[0].SubItems[1].Text;
            string name = PromptString("Rename board", cur);
            if (name == null! || name.Trim().Length == 0) return;
            _db.UpdateBoardName(id, name.Trim());
            if (_currentBoard?.Id == id) _currentBoard.Name = name.Trim();
            RefreshBoardList(); UpdateStatus();
        }

        private void LoadBoard(int boardId)
        {
            ClearCanvas();
            _currentBoard = _db.LoadAllBoards().FirstOrDefault(b => b.Id == boardId);
            if (_currentBoard == null) return;
            _nextZOrder = 0; _placeStep = 0;
            foreach (var item in _db.LoadItemsByBoard(boardId))
            {
                var imgPath = item.FullPath(_db.DataDir, boardId);
                if (!File.Exists(imgPath)) continue;
                Image img;
                try { using var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read); img = Image.FromStream(fs); }
                catch { continue; }
                CreateCard(img, item);
                if (item.ZOrder >= _nextZOrder) _nextZOrder = item.ZOrder + 1;
            }
            foreach (var c in _cards.Values.OrderBy(c => c.Item.ZOrder)) c.BringToFront();
            ApplyZoomToAllCards(); RefreshImageList(); UpdateStatus();
        }

        private void ClearCanvas()
        {
            foreach (var c in _cards.Values) { _canvas.Controls.Remove(c); c.Dispose(); }
            _cards.Clear(); _imageList.Items.Clear();
        }

        private void SelectBoardById(int id)
        {
            foreach (ListViewItem lvi in _boardList.Items)
                if ((int)lvi.Tag! == id) { lvi.Selected = true; lvi.EnsureVisible(); break; }
        }

        private void JoinBoard()
        {
            if (_currentUser == null)
            {
                MessageBox.Show("You must be logged in to join a board.", "Login Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dialog = new JoinBoardDialog(_db, _currentUser.Id);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                RefreshBoardList();
            }
            dialog.Dispose();
        }

        private void ShareBoard()
        {
            if (_boardList.SelectedItems.Count == 0) return;
            int boardId = (int)_boardList.SelectedItems[0].Tag!;
            
            if (_currentUser == null)
            {
                MessageBox.Show("You must be logged in to share a board.", "Login Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check if current user is the board owner
            var board = _db.LoadAllBoards().FirstOrDefault(b => b.Id == boardId);
            if (board == null || board.OwnerId != _currentUser.Id)
            {
                MessageBox.Show("Only the board owner can share this board.", "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var shareDialog = new ShareBoardDialog(_db, boardId, _currentUser.Id);
            shareDialog.ShowDialog();
            shareDialog.Dispose();
        }

        // ══════════════════════════════════════════════════════
        //  Image list
        // ══════════════════════════════════════════════════════
        private void ImageList_Jump()
        {
            if (_imageList.SelectedItems.Count == 0) return;
            int id = (int)_imageList.SelectedItems[0].Tag!;
            if (_cards.TryGetValue(id, out var card))
            { card.BringToFront(); _canvas.ScrollControlIntoView(card); }
        }

        private void ImageList_Rename()
        {
            if (_imageList.SelectedItems.Count == 0) return;
            int id = (int)_imageList.SelectedItems[0].Tag!;
            if (_cards.TryGetValue(id, out var card)) card.BeginRename();
        }

        private void ImageList_Delete()
        {
            if (_imageList.SelectedItems.Count == 0) return;
            int id = (int)_imageList.SelectedItems[0].Tag!;
            if (_cards.TryGetValue(id, out var card))
            {
                var r = MessageBox.Show(
                    $"Delete image #{id:D3} \"{card.Item.Name}\"?",
                    "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r == DialogResult.Yes) RemoveCard(card);
            }
        }

        // ══════════════════════════════════════════════════════
        //  Adding images
        // ══════════════════════════════════════════════════════
        private void OpenFileDialogUI()
        {
            if (!EnsureBoard()) return;
            using var dlg = new OpenFileDialog
            {
                Title = "Select image files",
                Filter = "Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.tiff)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.tiff|All files|*.*",
                Multiselect = true,
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                foreach (var f in dlg.FileNames) AddFromFile(f);
        }

        private void PasteFromClipboard()
        {
            if (!EnsureBoard()) return;
            if (Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();
                if (img != null) AddToBoard(img, "Pasted image");
            }
            else if (Clipboard.ContainsFileDropList())
            {
                foreach (string f in Clipboard.GetFileDropList()) AddFromFile(f);
            }
            else if (Clipboard.ContainsText())
            {
                var p = Clipboard.GetText().Trim().Trim('"');
                if (File.Exists(p)) AddFromFile(p);
                else Tip("No image found in clipboard");
            }
            else Tip("No image found in clipboard");
        }

        private bool EnsureBoard()
        {
            if (_currentBoard != null) return true;
            var r = MessageBox.Show(
                "No board is selected.\nWould you like to create a new board?",
                "Board Required", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) { CreateNewBoard(); return _currentBoard != null; }
            return false;
        }

        private void AddFromFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!ValidExts.Contains(ext)) { Tip($"Unsupported format: {ext}"); return; }
            try
            {
                Image img;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    img = Image.FromStream(fs);
                AddToBoard(img, Path.GetFileNameWithoutExtension(path));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddToBoard(Image image, string name)
        {
            if (_currentBoard == null) return;
            const int step = 28, maxOff = 196;
            int off = (_placeStep++ % (maxOff / step)) * step;
            int baseX = (int)((-_canvas.AutoScrollPosition.X + 24 + off) / _zoom);
            int baseY = (int)((-_canvas.AutoScrollPosition.Y + 24 + off) / _zoom);
            var (fw, fh) = Fit(image.Width, image.Height, 460, 388);

            var item = new BoardItem
            {
                BoardId = _currentBoard.Id,
                Name = name,
                FileName = "tmp",
                X = baseX,
                Y = baseY,
                Width = fw,
                Height = fh + 28,
                ZOrder = _nextZOrder++,
            };
            int id = _db.InsertItem(item); item.Id = id;

            var imagesDir = _currentBoard.ImagesDir(_db.DataDir);
            Directory.CreateDirectory(imagesDir);
            var fileName = $"{id}.png";
            image.Save(Path.Combine(imagesDir, fileName), ImageFormat.Png);
            item.FileName = fileName; _db.UpdateItemFileName(id, fileName);

            CreateCard(image, item); RefreshImageList(); UpdateStatus();
            if (_cards.TryGetValue(id, out var card)) _canvas.ScrollControlIntoView(card);
        }

        // ══════════════════════════════════════════════════════
        //  Card management
        // ══════════════════════════════════════════════════════
        private void CreateCard(Image image, BoardItem item)
        {
            var card = new ImageItemControl(image, item);
            card.Location = new Point((int)(item.X * _zoom), (int)(item.Y * _zoom));
            card.Size = new Size((int)(item.Width * _zoom), (int)(item.Height * _zoom));

            card.DeleteRequested += (_, _) => RemoveCard(card);
            card.BringToFrontRequested += (_, _) =>
            {
                card.BringToFront();
                card.Item.ZOrder = _nextZOrder++;
                _db.UpdateItemLayout(card.Item.Id, card.Item.X, card.Item.Y,
                    card.Item.Width, card.Item.Height, card.Item.ZOrder);
            };
            card.LayoutChanged += (_, _) =>
            {
                card.Item.X = (int)(card.Location.X / _zoom);
                card.Item.Y = (int)(card.Location.Y / _zoom);
                card.Item.Width = (int)(card.Width / _zoom);
                card.Item.Height = (int)(card.Height / _zoom);
                _db.UpdateItemLayout(card.Item.Id, card.Item.X, card.Item.Y,
                    card.Item.Width, card.Item.Height, card.Item.ZOrder);
            };
            card.NameChanged += (_, _) =>
            { _db.UpdateItemName(card.Item.Id, card.Item.Name); RefreshImageList(); };
            card.ForwardMouseWheelTo(_canvas, (s, e) =>
            {
                if (ModifierKeys.HasFlag(Keys.Control))
                    AdjustZoom(e.Delta > 0 ? +ZoomStep : -ZoomStep);
                else
                {
                    int ny = Math.Max(0, -_canvas.AutoScrollPosition.Y + (e.Delta > 0 ? -80 : 80));
                    _canvas.AutoScrollPosition = new Point(-_canvas.AutoScrollPosition.X, ny);
                }
            });
            _canvas.Controls.Add(card); card.BringToFront(); _cards[item.Id] = card;
        }

        // ══════════════════════════════════════════════════════
        //  Export board as PNG
        // ══════════════════════════════════════════════════════
        private void ExportBoardAsPng()
        {
            if (_cards.Count == 0)
            {
                MessageBox.Show("No images on this board.", "Export PNG",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Calculate bounding box of all cards (unzoomed coordinates)
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            foreach (var card in _cards.Values)
            {
                minX = Math.Min(minX, card.Item.X);
                minY = Math.Min(minY, card.Item.Y);
                maxX = Math.Max(maxX, card.Item.X + card.Item.Width);
                maxY = Math.Max(maxY, card.Item.Y + card.Item.Height);
            }

            const int padding = 40;
            int bmpW = maxX - minX + padding * 2;
            int bmpH = maxY - minY + padding * 2;

            // Let the user choose resolution
            using var optDlg = new ExportOptionsDialog(bmpW, bmpH,
                _currentBoard?.Name ?? "board");
            if (optDlg.ShowDialog() != DialogResult.OK) return;

            float scale = optDlg.Scale;
            int outW = (int)(bmpW * scale);
            int outH = (int)(bmpH * scale);

            using var bmp = new Bitmap(outW, outH);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            // Background
            g.Clear(optDlg.BackgroundColor);

            // Draw cards in Z order
            foreach (var card in _cards.Values.OrderBy(c => c.Item.ZOrder))
            {
                int cx = (int)((card.Item.X - minX + padding) * scale);
                int cy = (int)((card.Item.Y - minY + padding) * scale);
                int cw = (int)(card.Item.Width * scale);
                int ch = (int)(card.Item.Height * scale);

                // Card background
                using var cardBg = new SolidBrush(Color.FromArgb(50, 50, 53));
                g.FillRectangle(cardBg, cx, cy, cw, ch);

                // Title bar
                const int titleH = 28;
                int scaledTitleH = (int)(titleH * scale);
                using var titleBg = new SolidBrush(Color.FromArgb(28, 28, 30));
                g.FillRectangle(titleBg, cx, cy, cw, scaledTitleH);

                // ID badge
                using var badgeBg = new SolidBrush(Color.FromArgb(0, 120, 200));
                int badgeW = (int)(40 * scale);
                g.FillRectangle(badgeBg, cx, cy, badgeW, scaledTitleH);
                using var badgeFont = new Font("Consolas", Math.Max(6f, 7.5f * scale), FontStyle.Bold);
                using var whiteBrush = new SolidBrush(Color.White);
                var badgeSf = new System.Drawing.StringFormat
                {
                    Alignment = System.Drawing.StringAlignment.Center,
                    LineAlignment = System.Drawing.StringAlignment.Center,
                };
                g.DrawString($"#{card.Item.Id:D3}", badgeFont, whiteBrush,
                    new RectangleF(cx, cy, badgeW, scaledTitleH), badgeSf);

                // Title text
                using var nameFont = new Font("Segoe UI", Math.Max(6f, 8.5f * scale));
                using var nameBrush = new SolidBrush(Color.FromArgb(210, 210, 210));
                g.DrawString(card.Item.Name, nameFont, nameBrush,
                    new PointF(cx + badgeW + 4, cy + scaledTitleH / 2f - nameFont.Height / 2f));

                // Load and draw the image
                var imgPath = card.Item.FullPath(_db.DataDir, card.Item.BoardId);
                if (File.Exists(imgPath))
                {
                    try
                    {
                        using var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read);
                        using var img = Image.FromStream(fs);
                        g.DrawImage(img, new Rectangle(cx, cy + scaledTitleH, cw, ch - scaledTitleH));
                    }
                    catch { /* Skip images that fail to load */ }
                }

                // Border
                using var borderPen = new Pen(Color.FromArgb(75, 75, 80), Math.Max(1f, scale));
                g.DrawRectangle(borderPen, cx, cy, cw - 1, ch - 1);
            }

            // Save dialog
            string defaultName = (_currentBoard?.Name ?? "board")
                .Replace(" ", "_").Replace("/", "-");
            using var saveDlg = new SaveFileDialog
            {
                Title = "Save board as PNG",
                FileName = $"{defaultName}_{DateTime.Now:yyyyMMdd_HHmm}.png",
                Filter = "PNG image|*.png",
                DefaultExt = "png",
            };
            if (saveDlg.ShowDialog() != DialogResult.OK) return;

            bmp.Save(saveDlg.FileName, ImageFormat.Png);

            var result = MessageBox.Show(
                $"Saved:\n{saveDlg.FileName}\n\nOpen the file?",
                "Export Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (result == DialogResult.Yes)
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{saveDlg.FileName}\"");
        }

        private void RemoveCard(ImageItemControl card)
        {
            var imgPath = card.Item.FullPath(_db.DataDir, card.Item.BoardId);
            _canvas.Controls.Remove(card); card.Dispose(); _cards.Remove(card.Item.Id);
            _db.DeleteItem(card.Item.Id);
            try { if (File.Exists(imgPath)) File.Delete(imgPath); } catch { }
            RefreshImageList(); UpdateStatus();
        }

        // ══════════════════════════════════════════════════════
        //  List refresh helpers
        // ══════════════════════════════════════════════════════
        private void RefreshBoardList()
        {
            _boardList.BeginUpdate(); _boardList.Items.Clear();
            
            // Load boards for current user, or all boards if no user
            List<Board> boards = _currentUser != null 
                ? _db.LoadBoardsByUser(_currentUser.Id)
                : _db.LoadAllBoards();
            
            foreach (var b in boards)
            {
                var lvi = new ListViewItem($"#{b.Id:D3}");
                lvi.SubItems.Add(b.Name);
                lvi.Tag = b.Id;
                lvi.ForeColor = Color.FromArgb(200, 200, 200);
                if (_currentBoard?.Id == b.Id)
                { lvi.BackColor = Color.FromArgb(0, 80, 140); lvi.ForeColor = Color.White; }
                _boardList.Items.Add(lvi);
            }
            _boardList.EndUpdate();
        }

        private void RefreshImageList()
        {
            _imageList.BeginUpdate(); _imageList.Items.Clear();
            if (_currentBoard != null)
                foreach (var item in _db.LoadItemsByBoardOrderById(_currentBoard.Id))
                {
                    var lvi = new ListViewItem($"#{item.Id:D3}");
                    lvi.SubItems.Add(item.Name);
                    lvi.Tag = item.Id;
                    lvi.ForeColor = Color.FromArgb(200, 200, 200);
                    _imageList.Items.Add(lvi);
                }
            _imageList.EndUpdate();
        }

        private void UpdateStatus()
        {
            int n = _cards.Count;
            string board = _currentBoard != null
                ? $"Board #{_currentBoard.Id:D3} \"{_currentBoard.Name}\"  |  "
                : "(No board selected)  |  ";
            string zoom = $"  |  Zoom {(int)Math.Round(_zoom * 100)}%";
            _statusLbl.Text = n == 0
                ? $"{board}Ctrl+V to paste / drag & drop images{zoom}"
                : $"{board}{n} image{(n == 1 ? "" : "s")}  |  Drag: move  |  Edge: resize  |  Title DblClick: rename{zoom}";
        }

        private void Tip(string msg)
        {
            _tipLbl.Text = msg;
            var t = new System.Windows.Forms.Timer { Interval = 3000 };
            t.Tick += (_, _) => { _tipLbl.Text = ""; t.Stop(); t.Dispose(); };
            t.Start();
        }

        // ══════════════════════════════════════════════════════
        //  Utilities
        // ══════════════════════════════════════════════════════
        private static (int w, int h) Fit(int sw, int sh, int mw, int mh)
        {
            if (sw <= 0 || sh <= 0) return (mw, mh);
            if (sw <= mw && sh <= mh) return (sw, sh);
            double r = Math.Min((double)mw / sw, (double)mh / sh);
            return ((int)(sw * r), (int)(sh * r));
        }

        private static string PromptString(string prompt, string def)
        {
            var dlg = new Form
            {
                Text = prompt,
                Size = new Size(380, 130),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(42, 42, 45),
            };
            var tb = new TextBox
            {
                Text = def,
                Left = 12,
                Top = 12,
                Width = 340,
                BackColor = Color.FromArgb(55, 55, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
            };
            var ok = new Button
            {
                Text = "OK",
                Left = 12,
                Top = 48,
                Width = 80,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                DialogResult = DialogResult.OK,
            };
            var cancel = new Button
            {
                Text = "Cancel",
                Left = 100,
                Top = 48,
                Width = 100,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 70, 75),
                ForeColor = Color.White,
                DialogResult = DialogResult.Cancel,
            };
            ok.FlatAppearance.BorderSize = cancel.FlatAppearance.BorderSize = 0;
            dlg.Controls.AddRange(new Control[] { tb, ok, cancel });
            dlg.AcceptButton = ok; dlg.CancelButton = cancel; tb.SelectAll();
            return dlg.ShowDialog() == DialogResult.OK ? tb.Text : null!;
        }
    }
}