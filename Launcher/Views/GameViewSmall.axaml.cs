﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Launcher.Extensions;
using Launcher.Utils;
using LauncherGamePlugin.Commands;
using LauncherGamePlugin.Extensions;
using LauncherGamePlugin.Forms;
using LauncherGamePlugin.Interfaces;

namespace Launcher.Views;

public partial class GameViewSmall : UserControlExt<GameViewSmall>
{
    private IGame _game;

    [Binding(nameof(BottomPanel), "Background")]
    public IBrush HalfTransparency => new SolidColorBrush(new Color(128, 0, 0, 0));

    [Binding(nameof(GameLabel), "Content")]
    public string GameName => _game.Name;

    [Binding(nameof(SizeLabel), "Content")]
    public string GameSize => _game.ReadableSize();

    [Binding(nameof(ButtonPanel), "IsVisible")]
    public bool IsSelected => _isSelected;
    
    private bool _menuSet = false;
    private bool _isSelected = false;
    private bool _eventSpamPrevention = false;

    public GameViewSmall()
    {
        InitializeComponent();
    }

    public GameViewSmall(IGame game) : this()
    {
        _game = game;
        SetControls();
        UpdateView();
        Dispatcher.UIThread.Post(GetCoverImage);
    }

    public void Selected()
    {
        _isSelected = true;
        UpdateView();
        SetMenu();
    }

    public void Deselected()
    {
        _isSelected = false;
        UpdateView();
    }

    public async void GetCoverImage()
    {
        try
        {
            byte[]? img = await _game.CoverImage();

            if (img == null)
                throw new InvalidDataException();

            Stream stream = new MemoryStream(img);
            CoverImage.Source = Bitmap.DecodeToHeight(stream, 300);
        }
        catch
        {
            Loader.App.GetInstance().Logger.Log($"Failed to get cover of {_game.Name}");
        }
    }
    
    private void SetMenu()
    {
        List<Command> commands = _game.GetCommands();

        if (commands[0].Type != CommandType.Function)
            throw new InvalidDataException();

        Action actionOne = commands[0].Action;
        PrimaryButton.Command = new LambdaCommand(x => actionOne());
        PrimaryButtonLabel.Content = commands[0].Text;

        // I love hacky fixes for shit that doesn't work in avalonia
        commands.ForEach(x =>
        {
            if (x.Type == CommandType.Function)
            {
                Action originalAction = x.Action;
                x.Action = () =>
                {
                    if (!_eventSpamPrevention)
                        originalAction?.Invoke();

                    _eventSpamPrevention = !_eventSpamPrevention;
                    Menu.Close();
                };
            }
        });
        MoreMenu.Items = commands.Select(x => x.ToTemplatedControl());
    }
}