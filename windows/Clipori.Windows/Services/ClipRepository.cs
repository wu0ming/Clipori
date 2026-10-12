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
            """;
        cmd.ExecuteNonQuery();
        Prune();
    }

    private SqliteConnection Open() { var db = new SqliteConnection(ConnectionString); db.Open(); return db; }

    public List<ClipEntry> Load(int limit = 1000)
    {
        var output = new List<ClipEntry>();
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT id,kind,body,title,source,created_utc,hash,image_path,pinned FROM clips ORDER BY created_utc DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            output.Add(new ClipEntry {
                Id = r.GetString(0), Kind = r.GetString(1), Text = r.GetString(2),
                Title = r.GetString(3), Source = r.GetString(4),
                CreatedUtc = DateTime.Parse(r.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                Hash = r.GetString(6), ImagePath = r.IsDBNull(7) ? null : r.GetString(7), IsPinned = r.GetInt32(8) != 0
            });
        return output;
    }

    public void Add(ClipEntry item)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO clips(id,kind,body,title,source,created_utc,hash,image_path,pinned) VALUES($id,$kind,$body,$title,$source,$date,$hash,$image,$pinned)";
        cmd.Parameters.AddWithValue("$id", item.Id);
        cmd.Parameters.AddWithValue("$kind", item.Kind);
        cmd.Parameters.AddWithValue("$body", item.Text);
        cmd.Parameters.AddWithValue("$title", item.Title);
        cmd.Parameters.AddWithValue("$source", item.Source);
        cmd.Parameters.AddWithValue("$date", item.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$hash", item.Hash);
        cmd.Parameters.AddWithValue("$image", (object?)item.ImagePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pinned", item.IsPinned ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void SetPinned(ClipEntry item)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE clips SET pinned=$pinned WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", item.Id); cmd.Parameters.AddWithValue("$pinned", item.IsPinned ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void Delete(ClipEntry item)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM clips WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", item.Id); cmd.ExecuteNonQuery();
        if (item.Kind == "image" && !string.IsNullOrWhiteSpace(item.ImagePath))
        {
            try { File.Delete(item.ImagePath); } catch { /* An external image viewer may hold the file. */ }
        }
    }

    public void Prune(int retentionDays = 30)
    {
        var expiredImages = new List<string>();
        using (var db = Open())
        {
            string cutoff = DateTime.UtcNow.AddDays(-retentionDays).ToString("O");
            using (var query = db.CreateCommand())
            {
                query.CommandText = "SELECT image_path FROM clips WHERE pinned=0 AND created_utc < $cutoff AND image_path IS NOT NULL";
                query.Parameters.AddWithValue("$cutoff", cutoff);
                using var reader = query.ExecuteReader();
                while (reader.Read()) expiredImages.Add(reader.GetString(0));
            }
            using var cmd = db.CreateCommand();
            cmd.CommandText = "DELETE FROM clips WHERE pinned=0 AND created_utc < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", cutoff); cmd.ExecuteNonQuery();
        }
        foreach (var path in expiredImages) { try { File.Delete(path); } catch { } }
    }
}
