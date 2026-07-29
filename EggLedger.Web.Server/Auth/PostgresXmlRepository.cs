using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;

namespace EggLedger.Web.Server.Auth;

public sealed class PostgresXmlRepository(NpgsqlDataSource source) : IXmlRepository {
    public IReadOnlyCollection<XElement> GetAllElements() {
        var elements = new List<XElement>();
        using var cmd = source.CreateCommand("SELECT xml FROM data_protection_keys");
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            elements.Add(XElement.Parse(reader.GetString(0)));
        }
        return elements;
    }

    public void StoreElement(XElement element, string friendlyName) {
        using var cmd = source.CreateCommand(
            "INSERT INTO data_protection_keys (friendly_name, xml) VALUES ($1, $2)");
        cmd.Parameters.AddWithValue(string.IsNullOrEmpty(friendlyName) ? (object)DBNull.Value : friendlyName);
        cmd.Parameters.AddWithValue(element.ToString(SaveOptions.DisableFormatting));
        cmd.ExecuteNonQuery();
    }
}
