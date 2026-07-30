using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Imageboard_illust.Models;

namespace Imageboard_illust.Database
{
    /// <summary>
    /// SQLite ラッパー。
    ///
    /// テーブル構造:
    ///   users       (id PK, email UNIQUE, password_hash, created_at)
    ///   boards      (id PK, owner_id FK, name, board_type, is_public, created_at)
    ///   board_items (id PK, board_id FK, name, file_name, x, y, width, height, z_order, created_at)
    ///
    /// ファイル構造:
    ///   %AppData%\Imageboard_illust\
    ///   ├── board.db
    ///   └── boards\
    ///       ├── 1\images\1.png  ...
    ///       └── 2\images\3.png  ...
    /// </summary>
    public class AppDatabase : IDisposable
    {
        public string DataDir { get; }
        private readonly string _dbPath;
        private SqliteConnection? _conn;

        public AppDatabase()
        {
            DataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Imageboard_illust");
            _dbPath = Path.Combine(DataDir, "board.db");

            Directory.CreateDirectory(DataDir);
            Open();
            EnsureSchema();
        }

        private void Open()
        {
            _conn = new SqliteConnection($"Data Source={_dbPath}");
            _conn.Open();
            Exec("PRAGMA journal_mode=WAL;");
            Exec("PRAGMA synchronous=NORMAL;");
            Exec("PRAGMA foreign_keys=ON;");
        }

        private void EnsureSchema()
        {
            Exec(@"
                CREATE TABLE IF NOT EXISTS users (
                    id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    email           TEXT    NOT NULL UNIQUE,
                    password_hash   TEXT    NOT NULL,
                    reset_token     TEXT    DEFAULT NULL,
                    reset_token_expires TEXT DEFAULT NULL,
                    created_at      TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );
            ");

            Exec(@"
                CREATE TABLE IF NOT EXISTS boards (
                    id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    owner_id        INTEGER NOT NULL DEFAULT 1 REFERENCES users(id) ON DELETE CASCADE,
                    name            TEXT    NOT NULL DEFAULT '新しいボード',
                    board_type      TEXT    NOT NULL DEFAULT 'individual',
                    is_public       BOOLEAN NOT NULL DEFAULT 0,
                    created_at      TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );
            ");

            try { Exec("ALTER TABLE users ADD COLUMN reset_token TEXT DEFAULT NULL;"); } catch { }
            try { Exec("ALTER TABLE users ADD COLUMN reset_token_expires TEXT DEFAULT NULL;"); } catch { }
            try { Exec("ALTER TABLE boards ADD COLUMN owner_id INTEGER NOT NULL DEFAULT 1;"); } catch { }
            try { Exec("ALTER TABLE boards ADD COLUMN board_type TEXT NOT NULL DEFAULT 'individual';"); } catch { }
            try { Exec("ALTER TABLE boards ADD COLUMN is_public BOOLEAN NOT NULL DEFAULT 0;"); } catch { }

            Exec(@"
                CREATE TABLE IF NOT EXISTS board_items (
                    id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    board_id        INTEGER NOT NULL REFERENCES boards(id) ON DELETE CASCADE,
                    name            TEXT    NOT NULL DEFAULT '',
                    file_name       TEXT    NOT NULL DEFAULT '',
                    x               INTEGER NOT NULL DEFAULT 0,
                    y               INTEGER NOT NULL DEFAULT 0,
                    width           INTEGER NOT NULL DEFAULT 300,
                    height          INTEGER NOT NULL DEFAULT 250,
                    z_order         INTEGER NOT NULL DEFAULT 0,
                    created_at      TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );
            ");

            Exec(@"
                CREATE TABLE IF NOT EXISTS board_share_codes (
                    id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    board_id        INTEGER NOT NULL UNIQUE REFERENCES boards(id) ON DELETE CASCADE,
                    share_code      TEXT    NOT NULL UNIQUE,
                    created_at      TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );
            ");

            Exec(@"
                CREATE TABLE IF NOT EXISTS board_members (
                    id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    board_id        INTEGER NOT NULL REFERENCES boards(id) ON DELETE CASCADE,
                    user_id         INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    permission      TEXT    NOT NULL DEFAULT 'view',
                    joined_at       TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                    UNIQUE(board_id, user_id)
                );
            ");
        }

        public User? RegisterUser(string email, string password)
        {
            using (var cmd = Cmd("SELECT id FROM users WHERE email=$email;"))
            {
                cmd.Parameters.AddWithValue("$email", email);
                if (cmd.ExecuteScalar() != null) return null;
            }

            string passwordHash = HashPassword(password);
            using var cmd2 = Cmd("INSERT INTO users (email, password_hash) VALUES ($email, $hash); SELECT last_insert_rowid();");
            cmd2.Parameters.AddWithValue("$email", email);
            cmd2.Parameters.AddWithValue("$hash", passwordHash);
            int userId = Convert.ToInt32(cmd2.ExecuteScalar());

            CreateExampleBoard(userId);
            return GetUserById(userId);
        }

        public User? LoginUser(string email, string password)
        {
            var user = GetUserByEmail(email);
            if (user == null) return null;
            return VerifyPassword(password, user.PasswordHash) ? user : null;
        }

        public User? GetUserById(int userId)
        {
            using var cmd = Cmd("SELECT id, email, password_hash, created_at FROM users WHERE id=$id;");
            cmd.Parameters.AddWithValue("$id", userId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
                return new User
                {
                    Id = r.GetInt32(0),
                    Email = r.GetString(1),
                    PasswordHash = r.GetString(2),
                    CreatedAt = r.GetString(3)
                };
            return null;
        }

        public User? GetUserByEmail(string email)
        {
            using var cmd = Cmd("SELECT id, email, password_hash, created_at FROM users WHERE email=$email;");
            cmd.Parameters.AddWithValue("$email", email);
            using var r = cmd.ExecuteReader();
            if (r.Read())
                return new User
                {
                    Id = r.GetInt32(0),
                    Email = r.GetString(1),
                    PasswordHash = r.GetString(2),
                    CreatedAt = r.GetString(3)
                };
            return null;
        }

        private void CreateExampleBoard(int userId)
        {
            using var cmd = Cmd(@"
                INSERT INTO boards (owner_id, name, board_type, is_public)
                VALUES ($userId, $name, $type, $public);
            ");
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$name", "Example Board - Feel free to delete");
            cmd.Parameters.AddWithValue("$type", "individual");
            cmd.Parameters.AddWithValue("$public", 0);
            cmd.ExecuteNonQuery();
        }

        public string RequestPasswordReset(string email)
        {
            var user = GetUserByEmail(email);
            if (user == null) return null!;
            string resetToken = GenerateResetToken();
            string expiresAt = DateTime.Now.AddHours(24).ToString("yyyy-MM-dd HH:mm:ss");
            using var cmd = Cmd("UPDATE users SET reset_token=$token, reset_token_expires=$expires WHERE id=$id;");
            cmd.Parameters.AddWithValue("$token", resetToken);
            cmd.Parameters.AddWithValue("$expires", expiresAt);
            cmd.Parameters.AddWithValue("$id", user.Id);
            cmd.ExecuteNonQuery();
            return resetToken;
        }

        public bool ResetPassword(string resetToken, string newPassword)
        {
            using var cmd = Cmd(@"
                SELECT id, reset_token_expires FROM users 
                WHERE reset_token=$token;
            ");
            cmd.Parameters.AddWithValue("$token", resetToken);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return false;
            int userId = r.GetInt32(0);
            string expiresAt = r.GetString(1);
            if (DateTime.Parse(expiresAt) < DateTime.Now) return false;
            string passwordHash = HashPassword(newPassword);
            using var updateCmd = Cmd("UPDATE users SET password_hash=$hash, reset_token=NULL, reset_token_expires=NULL WHERE id=$id;");
            updateCmd.Parameters.AddWithValue("$hash", passwordHash);
            updateCmd.Parameters.AddWithValue("$id", userId);
            updateCmd.ExecuteNonQuery();
            return true;
        }

        private static string GenerateResetToken()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            byte[] tokenData = new byte[6];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenData);
            }
            var random = new Random(BitConverter.ToInt32(tokenData, 0));
            var result = new StringBuilder();
            for (int i = 0; i < 8; i++)
                result.Append(chars[random.Next(chars.Length)]);
            return result.ToString();
        }

        private static string GenerateShareCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            byte[] tokenData = new byte[6];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenData);
            }
            var random = new Random(BitConverter.ToInt32(tokenData, 0));
            var result = new StringBuilder();
            for (int i = 0; i < 6; i++)
                result.Append(chars[random.Next(chars.Length)]);
            return result.ToString();
        }

        public string CreateShareCode(int boardId)
        {
            string shareCode = GenerateShareCode();
            using var cmd = Cmd(@"
                INSERT INTO board_share_codes (board_id, share_code)
                VALUES ($bid, $code)
                ON CONFLICT(board_id) DO UPDATE SET share_code=$code;
            ");
            cmd.Parameters.AddWithValue("$bid", boardId);
            cmd.Parameters.AddWithValue("$code", shareCode);
            cmd.ExecuteNonQuery();
            return shareCode;
        }

        public string? GetBoardShareCode(int boardId)
        {
            using var cmd = Cmd("SELECT share_code FROM board_share_codes WHERE board_id=$bid;");
            cmd.Parameters.AddWithValue("$bid", boardId);
            return cmd.ExecuteScalar() as string;
        }

        public bool JoinBoard(string shareCode, int userId)
        {
            using (var cmd = Cmd("SELECT board_id FROM board_share_codes WHERE share_code=$code;"))
            {
                cmd.Parameters.AddWithValue("$code", shareCode);
                var result = cmd.ExecuteScalar();
                if (result == null) return false;
                int boardId = Convert.ToInt32(result);
                try
                {
                    using var insertCmd = Cmd(@"
                        INSERT INTO board_members (board_id, user_id, permission)
                        VALUES ($bid, $uid, 'edit');
                    ");
                    insertCmd.Parameters.AddWithValue("$bid", boardId);
                    insertCmd.Parameters.AddWithValue("$uid", userId);
                    insertCmd.ExecuteNonQuery();
                    return true;
                }
                catch { return false; }
            }
        }

        public List<(int UserId, string Email, string Permission)> GetBoardMembers(int boardId)
        {
            var members = new List<(int, string, string)>();
            using var cmd = Cmd(@"
                SELECT bm.user_id, u.email, bm.permission
                FROM board_members bm
                JOIN users u ON bm.user_id = u.id
                WHERE bm.board_id = $bid
                ORDER BY bm.joined_at;
            ");
            cmd.Parameters.AddWithValue("$bid", boardId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                members.Add((r.GetInt32(0), r.GetString(1), r.GetString(2)));
            }
            return members;
        }

        public void UpdateMemberPermission(int boardId, int userId, string permission)
        {
            using var cmd = Cmd(@"
                UPDATE board_members
                SET permission=$perm
                WHERE board_id=$bid AND user_id=$uid;
            ");
            cmd.Parameters.AddWithValue("$perm", permission);
            cmd.Parameters.AddWithValue("$bid", boardId);
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveBoardMember(int boardId, int userId)
        {
            using var cmd = Cmd(@"
                DELETE FROM board_members
                WHERE board_id=$bid AND user_id=$uid;
            ");
            cmd.Parameters.AddWithValue("$bid", boardId);
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.ExecuteNonQuery();
        }

        public string? GetUserPermissionForBoard(int boardId, int userId, int boardOwnerId)
        {
            if (userId == boardOwnerId) return "edit";
            using var cmd = Cmd("SELECT permission FROM board_members WHERE board_id=$bid AND user_id=$uid;");
            cmd.Parameters.AddWithValue("$bid", boardId);
            cmd.Parameters.AddWithValue("$uid", userId);
            return cmd.ExecuteScalar() as string;
        }

        public bool CanUserAccessBoard(int boardId, int userId)
        {
            using (var cmd = Cmd("SELECT owner_id FROM boards WHERE id=$bid;"))
            {
                cmd.Parameters.AddWithValue("$bid", boardId);
                var result = cmd.ExecuteScalar();
                if (result == null) return false;
                if (userId == Convert.ToInt32(result)) return true;
            }
            using (var cmd = Cmd("SELECT 1 FROM board_members WHERE board_id=$bid AND user_id=$uid;"))
            {
                cmd.Parameters.AddWithValue("$bid", boardId);
                cmd.Parameters.AddWithValue("$uid", userId);
                return cmd.ExecuteScalar() != null;
            }
        }

        public List<Board> LoadBoardsByUser(int userId)
        {
            var list = new List<Board>();
            using var cmd = Cmd(@"
                SELECT DISTINCT b.id, b.owner_id, b.name, b.board_type, b.is_public, b.created_at
                FROM boards b
                LEFT JOIN board_members bm ON b.id = bm.board_id
                WHERE b.owner_id=$uid OR bm.user_id=$uid
                ORDER BY b.id;
            ");
            cmd.Parameters.AddWithValue("$uid", userId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadBoard(r));
            return list;
        }

        public List<Board> LoadAllBoards()
        {
            var list = new List<Board>();
            using var cmd = Cmd("SELECT id, owner_id, name, board_type, is_public, created_at FROM boards ORDER BY id;");
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadBoard(r));
            return list;
        }

        public int InsertBoard(int userId, string name, string boardType = "individual", bool isPublic = false)
        {
            using var cmd = Cmd(@"
                INSERT INTO boards (owner_id, name, board_type, is_public)
                VALUES ($uid, $n, $type, $public);
                SELECT last_insert_rowid();
            ");
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$type", boardType);
            cmd.Parameters.AddWithValue("$public", isPublic ? 1 : 0);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void UpdateBoardName(int boardId, string name)
        {
            using var cmd = Cmd("UPDATE boards SET name=$n WHERE id=$id;");
            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$id", boardId);
            cmd.ExecuteNonQuery();
        }

        public void UpdateBoardType(int boardId, string boardType, bool isPublic)
        {
            using var cmd = Cmd("UPDATE boards SET board_type=$type, is_public=$public WHERE id=$id;");
            cmd.Parameters.AddWithValue("$type", boardType);
            cmd.Parameters.AddWithValue("$public", isPublic ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", boardId);
            cmd.ExecuteNonQuery();
        }

        public void DeleteBoard(int boardId)
        {
            using var cmd = Cmd("DELETE FROM boards WHERE id=$id;");
            cmd.Parameters.AddWithValue("$id", boardId);
            cmd.ExecuteNonQuery();
        }

        private static Board ReadBoard(SqliteDataReader r) => new()
        {
            Id = r.GetInt32(0),
            OwnerId = r.GetInt32(1),
            Name = r.GetString(2),
            BoardType = r.GetString(3),
            IsPublic = r.GetInt32(4) != 0,
            CreatedAt = r.GetString(5)
        };

        public List<BoardItem> LoadItemsByBoard(int boardId)
        {
            var list = new List<BoardItem>();
            using var cmd = Cmd(@"
                SELECT id, board_id, name, file_name, x, y, width, height, z_order, created_at
                FROM board_items
                WHERE board_id=$bid
                ORDER BY z_order;
            ");
            cmd.Parameters.AddWithValue("$bid", boardId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadItem(r));
            return list;
        }

        public List<BoardItem> LoadItemsByBoardOrderById(int boardId)
        {
            var list = new List<BoardItem>();
            using var cmd = Cmd(@"
                SELECT id, board_id, name, file_name, x, y, width, height, z_order, created_at
                FROM board_items
                WHERE board_id=$bid
                ORDER BY id;
            ");
            cmd.Parameters.AddWithValue("$bid", boardId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadItem(r));
            return list;
        }

        public int InsertItem(BoardItem item)
        {
            using var cmd = Cmd(@"
                INSERT INTO board_items (board_id, name, file_name, x, y, width, height, z_order)
                VALUES ($bid, $name, $fn, $x, $y, $w, $h, $z);
                SELECT last_insert_rowid();
            ");
            cmd.Parameters.AddWithValue("$bid",  item.BoardId);
            cmd.Parameters.AddWithValue("$name", item.Name);
            cmd.Parameters.AddWithValue("$fn",   item.FileName);
            cmd.Parameters.AddWithValue("$x",    item.X);
            cmd.Parameters.AddWithValue("$y",    item.Y);
            cmd.Parameters.AddWithValue("$w",    item.Width);
            cmd.Parameters.AddWithValue("$h",    item.Height);
            cmd.Parameters.AddWithValue("$z",    item.ZOrder);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void UpdateItemFileName(int id, string fileName)
        {
            using var cmd = Cmd("UPDATE board_items SET file_name=$fn WHERE id=$id;");
            cmd.Parameters.AddWithValue("$fn",  fileName);
            cmd.Parameters.AddWithValue("$id",  id);
            cmd.ExecuteNonQuery();
        }

        public void UpdateItemName(int id, string name)
        {
            using var cmd = Cmd("UPDATE board_items SET name=$name WHERE id=$id;");
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$id",   id);
            cmd.ExecuteNonQuery();
        }

        public void UpdateItemLayout(int id, int x, int y, int width, int height, int zOrder)
        {
            using var cmd = Cmd(@"
                UPDATE board_items
                SET x=$x, y=$y, width=$w, height=$h, z_order=$z
                WHERE id=$id;
            ");
            cmd.Parameters.AddWithValue("$x",  x);
            cmd.Parameters.AddWithValue("$y",  y);
            cmd.Parameters.AddWithValue("$w",  width);
            cmd.Parameters.AddWithValue("$h",  height);
            cmd.Parameters.AddWithValue("$z",  zOrder);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteItem(int id)
        {
            using var cmd = Cmd("DELETE FROM board_items WHERE id=$id;");
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        private static BoardItem ReadItem(SqliteDataReader r) => new()
        {
            Id        = r.GetInt32(0),
            BoardId   = r.GetInt32(1),
            Name      = r.GetString(2),
            FileName  = r.GetString(3),
            X         = r.GetInt32(4),
            Y         = r.GetInt32(5),
            Width     = r.GetInt32(6),
            Height    = r.GetInt32(7),
            ZOrder    = r.GetInt32(8),
            CreatedAt = r.GetString(9),
        };

        private static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private static bool VerifyPassword(string password, string hash)
        {
            string hashOfInput = HashPassword(password);
            return hashOfInput == hash;
        }

        private void Exec(string sql) { using var c = Cmd(sql); c.ExecuteNonQuery(); }

        private SqliteCommand Cmd(string sql)
        {
            var c = _conn!.CreateCommand();
            c.CommandText = sql;
            return c;
        }

        public void Dispose() { _conn?.Close(); _conn?.Dispose(); }
    }
}
