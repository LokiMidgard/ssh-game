namespace SshGame.Game.Screens;

internal class Wizzard : Screen
{
    private readonly (Action<ConsoleInput> caption, WizzardItem item)[] items;
    private int currntIndex;
    private int offset;

    public Wizzard(params (Action<ConsoleInput> caption, WizzardItem item)[] items)
    {
        this.items = items;
    }

    public override async Task Enter(int width, int height, PlayerConsole player, ConsoleInput console)
    {
        await base.Enter(width, height, player, console);
        console.ClearScreen();

        console.SetCursorPosition(1, 1);
        items[currntIndex].caption(console);
        await items[currntIndex].item.Repaint(console, currntIndex * 3 + 2, this.Height - currntIndex * 3 + 1, true);
    }

    private async Task Refresh(ConsoleInput console)
    {
        console.ClearScreen();
        for (int i = 0; i < currntIndex + 1; i++)
        {
            var position = i * 3 + 1 - offset;
            if (position > 0)
            {
                console.SetCursorPosition(position, 1);
                items[i].caption(console);
                await items[i].item.Repaint(console, position + 1, this.Height - position, true);
            }
        }

    }

    public override async Task HandleInput(ConsoleInput console, byte[] input)
    {
        await base.HandleInput(console, input);

        await items[currntIndex].item.HandleInput(console, input, currntIndex * 3 + 2 - offset, this.Height - (currntIndex * 3 + 2 - offset), true);

        var end = AnsiSequenceParser.ParseAnsiSequences(input, console.Encoding).OfType<LineSequence>().Any(x => x.Type == LineSequenceType.LineBreak);
        if (end)
        {
            items[currntIndex].item.IsSelected = true;
            await items[currntIndex].item.Repaint(console, currntIndex * 3 + 2 - offset, this.Height - (currntIndex * 3 + 2 - offset), true);

            currntIndex++;
            if (this.Height - (currntIndex * 3 + 1 - offset) < 3)
            {
                offset += 3 - (this.Height - (currntIndex * 3 + 1 - offset));
                await Refresh(console);
            }
            else
            {

                console.SetCursorPosition(currntIndex * 3 + 1 - offset, 1);
                items[currntIndex].caption(console);
                await items[currntIndex].item.Repaint(console, currntIndex * 3 + 2 - offset, this.Height - (currntIndex * 3 + 2 - offset), true);

            }

            // TODO: Switch to next
        }
    }



    internal abstract class WizzardItem
    {
        public bool IsSelected { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="console"></param>
        /// <param name="y">The one based from which line to paint</param>
        /// <param name="height">The nuber of Rows to Paint, <c>null</c> till end</param>
        /// <returns></returns>
        public virtual Task Repaint(ConsoleInput console, int y, int height, bool tillEnd)
        {
            console.SetCursorPosition(y, 1);
            if (tillEnd)
            {
                console.ClearToEndOfScreen();
            }
            else
            {
                for (int i = 0; i < height; i++)
                {
                    console.ClearLine().MoveCursorDown();
                }
                console.SetCursorPosition(y, 1);
            }

            return Task.CompletedTask;
        }

        public virtual Task HandleInput(ConsoleInput console, byte[] input, int y, int height, bool tillEnd) { return Task.CompletedTask; }
    }




    internal class SelectionScreen : WizzardItem
    {
        private readonly string[] values;
        private int selectedIndex = 0;

        public SelectionScreen(params string[] values)
        {
            this.values = values;
        }

        public override async Task Repaint(ConsoleInput console, int y, int height, bool tillEnd)
        {
            await base.Repaint(console, y, height, tillEnd);
            if (this.IsSelected)
            {
                console.Append("   ").Append(this.values[selectedIndex]);
            }
            else
            {
                if (height >= this.values.Length)
                {
                    // draw all 
                    for (int i = 0; i < this.values.Length; i++)
                    {
                        console.SetCursorPosition(i + y, 2);
                        if (i == this.selectedIndex)
                        {
                            console.Append("> ");
                            console.Color(ConsoleInput.SafeColors.BrightCyan)
                            .Bold();
                        }
                        else
                        {
                            console.Reset();
                            console.Append("  ");
                        }
                        console.Append(this.values[i]).Reset();
                    }
                }
                else
                {
                    var offset = height >= 3 ? selectedIndex - 1 : selectedIndex;
                    offset = Math.Clamp(offset, 0, this.values.Length - height);
                    // only draw what can fit
                    for (int i = offset; i < height + offset; i++)
                    {
                        console.SetCursorPosition(i + y - offset, 2);
                        if (i == this.selectedIndex)
                        {
                            console.Append("> ");
                            console.Color(ConsoleInput.SafeColors.BrightCyan)
                            .Bold();
                        }
                        else
                        {
                            console.Reset();
                            console.Append("  ");
                        }
                        console.Append(this.values[i]).Reset();
                    }
                }
            }
        }


        public override async Task HandleInput(ConsoleInput console, byte[] input, int y, int height, bool tillEnd)
        {
            foreach (var sequence in AnsiSequenceParser.ParseAnsiSequences(input, console.Encoding))
            {
                if (sequence is AnsiSequence ansiSequence)
                {
                    if (ansiSequence.Type == AnsiSequenceType.CursorUp)
                    {
                        selectedIndex--;
                    }
                    if (ansiSequence.Type == AnsiSequenceType.CursorDown)
                    {
                        selectedIndex++;
                    }
                    selectedIndex = Math.Clamp(selectedIndex, 0, values.Length - 1);
                    await Repaint(console, y, height, tillEnd);
                }
                if (sequence is LineSequence lineSequence)
                {
                    if (lineSequence.Type == LineSequenceType.Tab)
                    {
                        selectedIndex++;
                        selectedIndex %= values.Length;
                    }
                }
            }

        }

    }
}
