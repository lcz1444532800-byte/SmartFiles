using System.Text.Json;
namespace SmartFiles.Core;
public static class Json {
 public static readonly JsonSerializerOptions Options=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
 public static string Encode<T>(T value)=>JsonSerializer.Serialize(value,Options);
 public static T Decode<T>(string value)=>JsonSerializer.Deserialize<T>(value,Options)??throw new InvalidDataException("Invalid JSON");
}
