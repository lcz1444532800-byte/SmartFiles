using Microsoft.Data.Sqlite;
using System.IO;
namespace SmartFiles.Core;
public sealed class HistoryService : IDisposable {
 int retentionCount=10000,retentionDays=180;
 public void ConfigureRetention(int count,int days){retentionCount=count;retentionDays=days;}
 readonly SqliteConnection db; readonly object gate=new();
 public HistoryService(string home) {
  Directory.CreateDirectory(Path.Combine(home,"Data"));
  db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(home,"Data","smartfiles.db"),Mode=SqliteOpenMode.ReadWriteCreate}.ToString());db.Open();
  using var c=db.CreateCommand();c.CommandText="PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS records(kind TEXT NOT NULL,id TEXT NOT NULL,json TEXT NOT NULL,PRIMARY KEY(kind,id));";c.ExecuteNonQuery();using var index=db.CreateCommand();index.CommandText="CREATE INDEX IF NOT EXISTS records_time ON records(kind,json_extract(json,'$.Time') DESC)";index.ExecuteNonQuery();
 }
 public void Put<T>(string kind,string id,T value){lock(gate){using var c=db.CreateCommand();c.CommandText="INSERT INTO records(kind,id,json) VALUES($k,$i,$j) ON CONFLICT(kind,id) DO UPDATE SET json=excluded.json";c.Parameters.AddWithValue("$k",kind);c.Parameters.AddWithValue("$i",id);c.Parameters.AddWithValue("$j",Json.Encode(value));c.ExecuteNonQuery();if((value is MoveRecord m&&m.State is "Completed" or "Rollback" or "Undone")||value is LearnedRule){if(Count(kind)>retentionCount)Prune(retentionCount,retentionDays);}}}
 public List<T> Read<T>(string kind){lock(gate){using var c=db.CreateCommand();c.CommandText="SELECT json FROM records WHERE kind=$k";c.Parameters.AddWithValue("$k",kind);using var r=c.ExecuteReader();var a=new List<T>();while(r.Read())a.Add(Json.Decode<T>(r.GetString(0)));return a;}}
 public List<MoveRecord> RecentMoves(int count=1000){lock(gate){using var c=db.CreateCommand();c.CommandText="SELECT json FROM records WHERE kind='transaction' ORDER BY json_extract(json,'$.Time') DESC LIMIT $n";c.Parameters.AddWithValue("$n",Math.Clamp(count,1,1000));using var r=c.ExecuteReader();var a=new List<MoveRecord>();while(r.Read())a.Add(Json.Decode<MoveRecord>(r.GetString(0)));return a;}}
 public T? Find<T>(string kind,string id){lock(gate){using var c=db.CreateCommand();c.CommandText="SELECT json FROM records WHERE kind=$k AND id=$i";c.Parameters.AddWithValue("$k",kind);c.Parameters.AddWithValue("$i",id);var value=c.ExecuteScalar() as string;return value==null?default:Json.Decode<T>(value);}}
 public long Count(string kind){lock(gate){using var c=db.CreateCommand();c.CommandText="SELECT count(*) FROM records WHERE kind=$k";c.Parameters.AddWithValue("$k",kind);return Convert.ToInt64(c.ExecuteScalar());}}
 public void Delete(string kind,string id){lock(gate){using var c=db.CreateCommand();c.CommandText="DELETE FROM records WHERE kind=$k AND id=$i";c.Parameters.AddWithValue("$k",kind);c.Parameters.AddWithValue("$i",id);c.ExecuteNonQuery();}}
 public void Prune(int maxCount,int days){lock(gate){
  using var tx=db.BeginTransaction();using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="DELETE FROM records WHERE kind='transaction' AND json_extract(json,'$.State') IN ('Completed','Rollback','Undone') AND (json_extract(json,'$.Time') < $cut OR id IN (SELECT id FROM records WHERE kind='transaction' AND json_extract(json,'$.State') IN ('Completed','Rollback','Undone') ORDER BY json_extract(json,'$.Time') DESC LIMIT -1 OFFSET $count)); DELETE FROM records WHERE kind='rule' AND (json_extract(json,'$.LastUsed') < $cut OR id IN (SELECT id FROM records WHERE kind='rule' ORDER BY json_extract(json,'$.LastUsed') DESC LIMIT -1 OFFSET $count));";c.Parameters.AddWithValue("$cut",DateTime.UtcNow.AddDays(-days).ToString("O"));c.Parameters.AddWithValue("$count",maxCount);c.ExecuteNonQuery();tx.Commit();
  using var pages=db.CreateCommand();pages.CommandText="PRAGMA page_count";long count=Convert.ToInt64(pages.ExecuteScalar());pages.CommandText="PRAGMA freelist_count";long free=Convert.ToInt64(pages.ExecuteScalar());
  // No periodic write loop: compact only after significant reusable space has accumulated.
  if(count>4096&&free>count/2){using var vacuum=db.CreateCommand();vacuum.CommandText="PRAGMA wal_checkpoint(TRUNCATE); VACUUM;";try{vacuum.ExecuteNonQuery();}catch(SqliteException){}}
 }}
 public void Dispose(){lock(gate)db.Dispose();}
}
