﻿using System;
using Spectre.Console;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TerminalGPT.Constants;
using TerminalGPT.Options;
using Spectre.Console.Rendering;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using TerminalGPT.Data;
using TerminalGPT.Enums;
using TerminalGPT.Extensions;

namespace TerminalGPT.Services;

public interface ITerminalChatService : IBaseService
{
    Task Help();
    Task PrintCommandNotImplemented();
}

public class TerminalChatService : BaseService, ITerminalChatService
{
    private readonly IChatService _chatService;
    private readonly IOpenAIService _openAiService;
    private readonly TerminalGptOptions _options;
    private List<IRenderable> items;
    private readonly IChatCommandService _commandService;
    private readonly IUserInputService _userInputService;

    public TerminalChatService(
        IChatService chatService,
        IOpenAIService openAiService,
        IChatCommandService commandService,
        IOptions<TerminalGptOptions> options,
        IUserInputService userInputService
    )
    {
        _chatService = chatService;
        _openAiService = openAiService;
        _commandService = commandService;
        _userInputService = userInputService;
        _options = options.Value;
        items = new List<IRenderable>();
    }

    protected override async Task<ExitCode.Code> Run()
    {
        DrawHeader();

        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                AnsiConsole.Background = Color.DarkBlue;
                var input = string.Empty;
                
                
                // Hide the cursor
                Console.CursorVisible = false;
                while (string.IsNullOrWhiteSpace(input = await _userInputService.GetInputWithLiveDisplay()))
                {
                    await Task.Delay(1);
                }
                // Show the cursor
                Console.CursorVisible = true;

                if (_commandService.TryParseCommand(input, out var command))
                {
                    await _commandService.ExecuteCommand(command);
                    DrawCommand(command);
                    if (command.CommandEnum == CommandEnum.Command.Exit)
                    {
                        return ExitCode.Code.CleanExit;
                    }
                }
                else
                {
                    AddItemToDisplay(input, $"{Environment.UserName} - {DateTime.Now}", true);

                    await AnsiConsole.Status().Spinner(Spinner.Known.Star)
                        .StartAsync("[cyan][bold]TerminalGPT[/][/] is thinking...", async spinnerCtx =>
                        {
                            await _chatService.AddMessageToCurrentThread(input, "User");

                            spinnerCtx.Status = "[cyan][bold]TerminalGPT[/][/] is done thinking...";
                            spinnerCtx.Spinner = Spinner.Known.Star2;
                            spinnerCtx.SpinnerStyle = Style.Parse("green");
                            spinnerCtx.Refresh();

                            Task.Delay(1000).Wait();
                        });

                    var response = _chatService.CurrentThread.Messages.Last().Message.Content;

                    AddItemToDisplay(response, $"TerminalGPT - {DateTime.Now}");
                }
            }

            return ExitCode.Code.Success;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return ExitCode.Code.Error;
        }
    }

    private void DrawCommand(ChatCommand command)
    {
        AddItemToDisplay(command.CommandEnum.GetDescription(), $"Command::[gold3][bold]{command.CommandEnum.ToString()}[/][/] - {DateTime.Now}", true);
    }

    public void DrawHeader()
    {
        AnsiConsole.Write(new FigletText(
                FigletFont.Parse(AppConstants.FigletFont),
                "T e r m i n a l . G P T")
            .Centered()
            .Color(Color.Aquamarine3)
        );

        AnsiConsole.Render(
            new Table().Border(TableBorder.Rounded)
                .AddColumn("Version")
                .AddColumn("GitHub Repo")
                .AddRow("[red][bold]1.0.0[/][/]",
                    "[blue][link=https://github.com/LemonDrop1228/Terminal.GPT]https://github.com/LemonDrop1228/Terminal.GPT[/][/]")
                .Centered());
        AnsiConsole.Render(new Rule());
    }

    public void AddItemToDisplay(string content, string header, bool isUser = false)
    {
        try
        {
            items.Add(new Panel($"{content}").Header(isUser
                ? $"[green][bold]{header}[/][/]"
                : $"[cyan][bold]{header}[/][/]"));
        }
        catch (Exception e)
        {
            items.Add(new Panel($"{ScrubMarkup(content)}").Header(isUser
                ? $"[green][bold]{header}[/][/]"
                : $"[cyan][bold]{header}[/][/]"));
        }
        
        items.Add(new Rule().RuleStyle(Style.Parse("cyan")));
        DrawItems();
    }

    private string ScrubMarkup(string content)
    {
        var regex = new Regex(@"\[(.*?)\]");
        content = regex.Replace(content, "");
        return content;
    }

    public void DrawItems()
    {
        AnsiConsole.Clear();
        DrawHeader();

        foreach (var item in items)
        {
            AnsiConsole.Render(item);
        }
    }

    public async Task Help()
    {
        throw new NotImplementedException();
    }

    public async Task PrintCommandNotImplemented()
    {
        await AnsiConsole.Status().Spinner(Spinner.Known.Star)
            .StartAsync("[red][bold]Command not implemented[/][/]", async spinnerCtx =>
            {
                spinnerCtx.Spinner = Spinner.Known.Star2;
                spinnerCtx.SpinnerStyle = Style.Parse("Yellow");
                spinnerCtx.Refresh();
                Task.Delay(1000).Wait();
            });
    }
}