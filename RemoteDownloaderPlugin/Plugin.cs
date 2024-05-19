﻿using LauncherGamePlugin;
using LauncherGamePlugin.Commands;
using LauncherGamePlugin.Enums;
using LauncherGamePlugin.Forms;
using LauncherGamePlugin.Interfaces;
using Newtonsoft.Json;
using RemoteDownloaderPlugin.Game;
using RemoteDownloaderPlugin.Gui;

namespace RemoteDownloaderPlugin;

public class Plugin : IGameSource
{
    public string ServiceName => "Remote Games Integration";
    public string Version => "v1.0.0";
    public string SlugServiceName => "remote-games";
    public string ShortServiceName => "Remote";
    public PluginType Type => PluginType.GameSource;
    
    public Storage<Store> Storage { get; private set; }
    public IApp App { get; private set; }

    private Remote _cachedRemote = new();
    private List<OnlineGame> _onlineGames = new();
    
    public async Task<InitResult?> Initialize(IApp app)
    {
        App = app;
        Storage = new(app, "remote_games.json");
        await FetchRemote();
        
        return null;
    }

    public List<Command> GetGameCommands(IGame game)
    {
        if (game is OnlineGame onlineGame)
        {
            if (onlineGame.ProgressStatus != null)
            {
                return new()
                {
                    new Command("Stop", () => onlineGame.Stop())
                };
            }
            else
            {
                return new()
                {
                    new Command("Install", () => onlineGame.Download())
                };
            }
        }

        if (game is InstalledGame installedGame)
        {
            return new()
            {
                new Command(game.IsRunning ? "Running" : "Launch", installedGame.Play),
                new Command("Open in File Manager", installedGame.OpenInFileManager),
                new Command($"Version: {installedGame.Game.Version}"),
                new Command("Uninstall", () =>         
                    App.Show2ButtonTextPrompt($"Do you want to uninstall {game.Name}?", "Yes", "No", _ =>
                    {
                        installedGame.Delete();
                        App.HideForm();
                    }, _ => App.HideForm(), game))
            };
        }

        throw new NotImplementedException();
    }
    
    public async Task<List<IGame>> GetGames()
    {
        List<InstalledGame> installedGames = Storage.Data.EmuGames.Select(x => new InstalledGame(x, this))
            .Concat(Storage.Data.PcGames.Select(x => new InstalledGame(x, this))).ToList();

        List<string> installedIds = installedGames.Select(x => x.Game.Id).ToList();

        return _onlineGames.Where(x => !installedIds.Contains(x.Entry.GameId))
            .Select(x => (IGame)x)
            .Concat(installedGames.Select(x => (IGame)x))
            .ToList();
    }
    
    public async Task<bool> FetchRemote()
    {
        if (string.IsNullOrEmpty(Storage.Data.IndexUrl))
            return false;

        try
        {
            using HttpClient client = new();
            var data = await client.GetStringAsync(Storage.Data.IndexUrl);
            _cachedRemote = JsonConvert.DeserializeObject<Remote>(data)!;
            
            _onlineGames = _cachedRemote.Emu.Select(x => new OnlineGame(x, this))
                .Concat(_cachedRemote.Pc.Select(x => new OnlineGame(x, this))).ToList();
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public List<Command> GetGlobalCommands()
    {
        List<Command> commands = new()
        {
            new Command("Reload", Reload),
            new Command(),
            new Command("Edit Index URL", () => new SettingsRemoteIndexGui(App, this).ShowGui()),
            new Command("Add Emulation Profile", () => new AddOrEditEmuProfileGui(App, this).ShowGui()),
            new Command("Edit Emulation Profile", 
                Storage.Data.EmuProfiles.Select(
                    x => new Command($"Edit {x.Platform}", new AddOrEditEmuProfileGui(App, this, x).ShowGui)).ToList()),
            new Command("Delete Emulation Profile",
                Storage.Data.EmuProfiles.Select(
                    x => new Command($"Delete {x.Platform}", () =>
                    {
                        App.Show2ButtonTextPrompt($"Do you want to remove platform {x.Platform}?", "Yes", "No",
                            _ =>
                            {
                                Storage.Data.EmuProfiles.Remove(x);
                                Storage.Save();
                                App.ReloadGlobalCommands();
                                App.HideForm();
                            }, _ => App.HideForm());
                    })).ToList())
        };
        
        return commands;
    }
    
    private async void Reload()
    {
        App.ShowTextPrompt("Reloading Remote Games...");
        await FetchRemote();
        App.ReloadGames();
        App.HideForm();
    }
}