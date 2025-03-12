using System.Data;
using System.Numerics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Config;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using MySqlConnector;


namespace Iks_ASConvert;

public class Iks_ASConvert : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "Iks_ASConvert_MeowFix";

    public override string ModuleVersion => "0.0.2";
    public override string ModuleAuthor => "iks";

    private string _dbConnectionString = "";

    public PluginConfig Config { get; set; } = new PluginConfig();

    public void OnConfigParsed(PluginConfig config)
    {
        _dbConnectionString = "Server=" + config.host + ";Database=" + config.database
                              + ";port=" + config.port + ";User Id=" + config.user + ";password=" + config.pass;

        Task.Run(async () =>
        {
            await SetFlagsToAdmins();
        });
        Config = config;
    }

    public async Task SetFlagsToAdmins()
    {
        List<Admin> admins = new List<Admin>();
        string sql =
            "SELECT a.steamid, s.flags AS server_flags, g.flags AS group_flags, s.immunity, s.server_id " +
            "FROM as_admins a " +
            "JOIN as_admins_servers s ON a.id = s.admin_id " +
            "LEFT JOIN as_groups g ON s.group_id = g.id";

        try
        {
            using (var connection = new MySqlConnection(_dbConnectionString))
            {
                connection.Open();
                var comm = new MySqlCommand(sql, connection);
                var reader = await comm.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string steamid = reader.GetString("steamid");
                    string serverFlags = reader.GetString("server_flags");
                    string groupFlags = reader.IsDBNull(reader.GetOrdinal("group_flags")) ? "" : reader.GetString("group_flags");
                    int immunity = reader.GetInt32("immunity");
                    int serverId = reader.GetInt32("server_id");

                    string combinedFlags = serverFlags + groupFlags;

                    admins.Add(new Admin(steamid, combinedFlags, immunity, serverId));
                }
            }
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($" [Iks_AsConverter] Db error: {ex}");
        }

        Server.NextFrame(() =>
        {
            SetFlags(admins);
        });
    }
    public void SetFlags(List<Admin> admins)
    {
        foreach (var admin in admins)
        {
            if (admin.ServerId != Config.ServerId)
            {
                Console.WriteLine($"[Iks_AsConverter] Admin {admin.Steamid} skipped. ServerId {admin.ServerId} does not match configured ServerId {Config.ServerId}.");
                continue;
            }

            AdminManager.ClearPlayerPermissions(admin.Steamid);
            AdminManager.SetPlayerImmunity(admin.Steamid, 0);

            foreach (var flags in Config.ConvertFlags)
            {
                if (admin.Flags.Contains(flags.Key))
                {
                    AdminManager.AddPlayerPermissions(admin.Steamid, flags.Value.ToArray());
                    AdminManager.SetPlayerImmunity(admin.Steamid, admin.Immunity < 0 ? 0 : (uint)admin.Immunity);
                    Console.WriteLine($"[Iks_AsConverter] Admin {admin.Steamid} converted to {string.Join(", ", flags.Value)} on server {admin.ServerId}");
                }
            }
        }
    }
    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_as_convert")]
    public void OnConvertCommand(CCSPlayerController? controller, CommandInfo info)
    {
        Task.Run(async () =>
        {
            await SetFlagsToAdmins();
        });
    }
}
