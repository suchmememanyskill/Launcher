﻿using craftersmine.SteamGridDBNet;
using LauncherGamePlugin;
using LauncherGamePlugin.Forms;
using LauncherGamePlugin.Interfaces;
using SteamGridDbMiddleware.Model;

namespace SteamGridDbMiddleware.Gui;

public class OnBackgroundEdit
{
    public IGame Game { get; set; }
    public SteamGridDb Instance { get; set; }

    public OnBackgroundEdit(IGame game, SteamGridDb instance)
    {
        Game = game;
        Instance = instance;
    }

    public async void ShowGui()
    {
        var games = await Instance.Api.SearchForGamesAsync(Game.Name);

        List<SteamGridDbHero> covers = new();
        string gameName = "???";

        if (games.Length > 0)
        {
            var game = games.First();
            gameName = game.Name;
            covers = (await Instance.Api.GetHeroesForGameAsync(game,
                dimensions: SteamGridDbDimensions.W1920H620 | SteamGridDbDimensions.W3840H1240, types: SteamGridDbTypes.Static))?.ToList() ?? new();
        }

        List<FormEntry> entries = new();

        entries.Add(Form.TextBox($"Backgrounds for {gameName}", FormAlignment.Center, "Bold"));
        entries.Add(Form.Button("Back", _ => Instance.App.HideForm(), "Remove current background", _ => ClearBackground()));
        
        if (!HasBackground())
            entries.Last().ButtonList.Last().Action = null;
        
        foreach (var steamGridDbGrid in covers.Take(10))
        {
            entries.Add(Form.Image($"By {steamGridDbGrid.Author.Name}", () => Storage.ImageDownload(steamGridDbGrid.FullImageUrl), _ => SetBackground(steamGridDbGrid.Id.ToString(), steamGridDbGrid.FullImageUrl), FormAlignment.Center));
        }

        Instance.App.ShowForm(entries);
    }
    
    private void ClearBackground()
    {
        Instance.App.HideForm();
        Override? x = Instance.Storage.Data.GetBackground(Game);
        if (x != null)
        {
            Instance.Storage.Data.Backgrounds.Remove(x);
            Instance.Storage.Save();
            Instance.App.ReloadGames();
        }
    }

    private void SetBackground(string id, string url)
    {
        Instance.App.HideForm();
        Override? x = Instance.Storage.Data.GetBackground(Game);
        x ??= new(Game.Name, Game.Source.ServiceName, url, id);

        x.Url = url;
        x.Id = id;

        if (!Instance.Storage.Data.Backgrounds.Contains(x))
            Instance.Storage.Data.Backgrounds.Add(x);
        
        Instance.Storage.Save();
        Instance.App.ReloadGames();
    }

    private bool HasBackground()
        => Instance.Storage.Data.HasBackground(Game);
}