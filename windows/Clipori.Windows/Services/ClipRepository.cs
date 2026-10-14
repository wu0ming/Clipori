using System.IO;
using Microsoft.Data.Sqlite;
using Clipori.Windows.Models;

namespace Clipori.Windows.Services;

public sealed class ClipRepository
{
    public string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CliporiWindows");
    public string ImagesPath => Path.Combine(Root, "images");
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = Path.Combine(Root, "clipori.sqlite") }.ToString();

    public ClipRepository()
    {
        Directory.CreateDirectory(ImagesPath);
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS clips (
                id TEXT PRIMARY KEY, kind TEXT NOT NULL, body TEXT NOT NULL,
                title TEXT NOT NULL, source TEXT NOT NULL, created_utc TEXT NOT NULL,
                hash TEXT NOT NULL, image_path TEXT, pinned INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_clips_created ON clips(created_utc DESC);
            CREATE TABLE IF NOT EXISTS categories (name TEXT PRIMARY KEY, color TEXT NOT NULL DEFAULT '#AE90DB');
            """;
        cmd.ExecuteNonQuery();
        EnsureColumn(db, "category", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(db, "notes", "TEXT NOT NULL DEFAULT ''");
        using (var query = db.CreateCommand())
        {
            query.CommandText = "PRAGMA table_info(categories)";
            using var r = query.ExecuteReader();
            bool existing = false;
            while (r.Read()) if (r.GetString(1) == "pinboard") existing = true;
            r.Close();
            if (!existing)
            {
                using var alter = db.CreateCommand();
                alter.CommandText = "ALTER TABLE categories ADD COLUMN pinboard INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }
        }
    }

    private static void EnsureColumn(SqliteConnection db, string name, string definition)
    {
        using var check = db.CreateCommand();
        check.CommandText = "PRAGMA table_info(clips)";
        using var reader = check.ExecuteReader();
        bool found = false;
        while (reader.Read()) if (reader.GetString(1) == name) found = true;
        reader.Close();
        if (!found)
        {
            using var alter = db.CreateCommand();
            alter.CommandText = $"ALTER TABLE clips ADD COLUMN {name} {definition}";
            alter.ExecuteNonQuery();
        }
    }
    private SqliteConnection Open() { var db = new SqliteConnection(ConnectionString); db.Open(); return db; }

    public List<(string Name, string Color, bool Pinboard)> Categories()
    {
        var result = new List<(string, string, bool)>();
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "SELECT name,color,pinboard FROM categories ORDER BY rowid ASC";
        using var r = c.ExecuteReader();
        while (r.Read()) result.Add((r.GetString(0), r.GetString(1), r.GetInt32(2) != 0));
        return result;
    }
    public void AddCategory(string name, string color = "#AE90DB", bool pinboard = false)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "INSERT OR IGNORE INTO categories(name,color,pinboard) VALUES($name,$color,$pinboard)";
        c.Parameters.AddWithValue("$name", name); c.Parameters.AddWithValue("$color", color);
        c.Parameters.AddWithValue("$pinboard", pinboard ? 1 : 0);
        c.ExecuteNonQuery();
    }
    public void SetCategoryColor(string name, string color)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE categories SET color=$color WHERE name=$name";
        cmd.Parameters.AddWithValue("$name", name); cmd.Parameters.AddWithValue("$color", color);
        cmd.ExecuteNonQuery();
    }
    public void DeleteCategory(string name)
    {
        using var db = Open(); using var transaction = db.BeginTransaction();
        using var c = db.CreateCommand(); c.Transaction = transaction;
        c.CommandText = "UPDATE clips SET category='' WHERE category=$name; DELETE FROM categories WHERE name=$name";
        c.Parameters.AddWithValue("$name", name); c.ExecuteNonQuery(); transaction.Commit();
    }

    public List<ClipEntry> Load(int limit = 1000)
    {
        var result = new List<ClipEntry>();
        var legacy = new HashSet<string>();
        using (var db = Open())
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT id,kind,body,title,source,created_utc,hash,image_path,pinned,category,notes FROM clips ORDER BY created_utc DESC LIMIT $limit";
            cmd.Parameters.AddWithValue("$limit", limit);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                try
                {
                    string id = r.GetString(0), body = r.GetString(2), title = r.GetString(3), notes = r.GetString(10);
                    if (!Protection.IsProtected(body) || !Protection.IsProtected(title) || !Protection.IsProtected(notes))
                        legacy.Add(id);
                    result.Add(new ClipEntry {
                        Id = id, Kind = r.GetString(1),
                        Text = Protection.Unprotect(body),
                        Title = Protection.Unprotect(title), Source = r.GetString(4),
                        CreatedUtc = DateTime.Parse(r.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                        Hash = r.GetString(6), ImagePath = r.IsDBNull(7) ? null : r.GetString(7),
                        IsPinned = r.GetInt32(8) != 0, CategoryName = r.GetString(9),
                        Notes = Protection.Unprotect(notes)
                    });
                }
                catch (Exception ex) { Diagnostics.Error("repository.record-decrypt", ex); }
            }
        }
        // Migrate old plaintext rows and PNG images to user-scoped DPAPI encryption.
        bool converted = false;
        foreach (var entry in result)
        {
            try
            {
                bool oldImage = entry.Kind == "image" && entry.ImagePath != null &&
                    File.Exists(entry.ImagePath) && !entry.ImagePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase);
                if (!oldImage && !legacy.Contains(entry.Id)) continue;
                if (entry.Kind == "image" && entry.ImagePath != null &&
                    File.Exists(entry.ImagePath) && !entry.ImagePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                {
                    var destination = Path.Combine(ImagesPath, entry.Id + ".dat");
                    File.WriteAllBytes(destination, Protection.ProtectBytes(File.ReadAllBytes(entry.ImagePath)));
                    File.Delete(entry.ImagePath);
                    entry.ImagePath = destination;
                }
                Save(entry);
                converted = true;
            }
            catch (Exception ex) { Diagnostics.Error("repository.migrate", ex); }
        }
        if (converted)
        {
            // Compact unused SQLite pages after legacy plaintext migration.
            try { using var db = Open(); using var command = db.CreateCommand(); command.CommandText = "VACUUM"; command.ExecuteNonQuery(); }
            catch (Exception ex) { Diagnostics.Error("repository.vacuum", ex); }
        }
        return result;
    }

    public void Add(ClipEntry entry)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO clips(id,kind,body,title,source,created_utc,hash,image_path,pinned,category,notes)
            VALUES($id,$kind,$body,$title,$source,$date,$hash,$image,$pinned,$category,$notes)
            """;
        Bind(cmd, entry); cmd.ExecuteNonQuery();
    }
    public void Save(ClipEntry entry)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = """
            UPDATE clips SET kind=$kind,body=$body,title=$title,source=$source,hash=$hash,
            image_path=$image,pinned=$pinned,category=$category,notes=$notes WHERE id=$id
            """;
        Bind(cmd, entry); cmd.ExecuteNonQuery();
    }
    private static void Bind(SqliteCommand cmd, ClipEntry e)
    {
        cmd.Parameters.AddWithValue("$id", e.Id); cmd.Parameters.AddWithValue("$kind", e.Kind);
        cmd.Parameters.AddWithValue("$body", Protection.Protect(e.Text)); cmd.Parameters.AddWithValue("$title", Protection.Protect(e.Title));
        cmd.Parameters.AddWithValue("$source", e.Source); cmd.Parameters.AddWithValue("$date", e.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$hash", e.Hash); cmd.Parameters.AddWithValue("$image", (object?)e.ImagePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pinned", e.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("$category", e.CategoryName);
        cmd.Parameters.AddWithValue("$notes", Protection.Protect(e.Notes));
    }
    public void SetPinned(ClipEntry e) => Save(e);
    public void Delete(ClipEntry entry)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM clips WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", entry.Id); cmd.ExecuteNonQuery();
        if (entry.Kind == "image" && entry.ImagePath != null)
            try { File.Delete(entry.ImagePath); } catch (Exception ex) { Diagnostics.Error("repository.image-delete", ex); }
    }
    public void Prune(int retentionDays = 30)
    {
        if (retentionDays <= 0) return;
        var expiredImages = new List<string>();
        using (var db = Open())
        {
            string cutoff = DateTime.UtcNow.AddDays(-retentionDays).ToString("O");
            using (var query = db.CreateCommand())
            {
                query.CommandText = "SELECT image_path FROM clips WHERE pinned=0 AND category NOT IN (SELECT name FROM categories WHERE pinboard=1) AND created_utc < $cutoff AND image_path IS NOT NULL";
                query.Parameters.AddWithValue("$cutoff", cutoff);
                using var reader = query.ExecuteReader();
                while (reader.Read()) expiredImages.Add(reader.GetString(0));
            }
            using var cmd = db.CreateCommand();
            cmd.CommandText = "DELETE FROM clips WHERE pinned=0 AND category NOT IN (SELECT name FROM categories WHERE pinboard=1) AND created_utc < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", cutoff); cmd.ExecuteNonQuery();
        }
        foreach (var path in expiredImages) { try { File.Delete(path); } catch { } }
    }
}
