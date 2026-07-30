namespace Imageboard_illust.Models
{
    /// <summary>
    /// ユーザーアカウント
    /// users テーブルの1行
    /// </summary>
    public class User
    {
        public int    Id       { get; set; }
        public string Email    { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string CreatedAt { get; set; } = "";
    }

    /// <summary>
    /// イメージボード（1ボード = 1つの作業スペース）
    /// boards テーブルの1行
    /// </summary>
    public class Board
    {
        public int    Id        { get; set; }
        public int    OwnerId   { get; set; }  // User who created this board
        public string Name      { get; set; } = "";
        public string BoardType { get; set; } = "individual";  // "individual" or "shared"
        public bool   IsPublic  { get; set; } = false;  // Only relevant for individual boards
        public string CreatedAt { get; set; } = "";

        /// <summary>このボードの画像が格納されるフォルダ</summary>
        public string ImagesDir(string dataDir)
            => System.IO.Path.Combine(dataDir, "boards", Id.ToString(), "images");
    }

    /// <summary>
    /// ボード上の1枚の画像カード
    /// board_items テーブルの1行
    /// </summary>
    public class BoardItem
    {
        public int    Id        { get; set; }
        public int    BoardId   { get; set; }
        public string Name      { get; set; } = "";
        public string FileName  { get; set; } = "";
        public int    X         { get; set; }
        public int    Y         { get; set; }
        public int    Width     { get; set; }
        public int    Height    { get; set; }
        public int    ZOrder    { get; set; }
        public string CreatedAt { get; set; } = "";

        public string FullPath(string dataDir, int boardId)
            => System.IO.Path.Combine(dataDir, "boards", boardId.ToString(), "images", FileName);
    }
}
