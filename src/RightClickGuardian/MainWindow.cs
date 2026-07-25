using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RightClickGuardian
{
    public sealed class MainWindow : Window
    {
        private sealed class NavigationState
        {
            public string Category;
            public string SoftwareKey;
            public double VerticalOffset;

            public NavigationState(string category, string softwareKey,
                double verticalOffset)
            {
                Category = category ?? "";
                SoftwareKey = softwareKey ?? "";
                VerticalOffset = Math.Max(0, verticalOffset);
            }
        }

        private static readonly Color Accent = Color.FromRgb(124, 119, 255);
        private static readonly Color AccentDark = Color.FromRgb(91, 84, 220);
        private static readonly Color Pink = Color.FromRgb(255, 143, 181);
        private static readonly Color Mint = Color.FromRgb(80, 197, 160);
        private static readonly Color Ink = Color.FromRgb(41, 48, 73);
        private static readonly Color Muted = Color.FromRgb(119, 125, 151);
        private static readonly Color Surface = Color.FromRgb(255, 255, 255);
        private static readonly Color Page = Color.FromRgb(247, 248, 253);
        private static readonly Color Line = Color.FromRgb(233, 234, 244);

        private readonly PolicyStore policyStore;
        private readonly MenuScanner scanner;
        private readonly EnforcementService enforcement;
        private readonly ContextMenuLabService labService;
        private readonly Dictionary<string, Button> navButtons;
        private readonly Dictionary<string, TextBlock> navCounts;
        private List<MenuEntry> allEntries;
        private ScanResult lastScan;
        private string selectedCategory;
        private bool scanning;
        private LabSample selectedLabSample;
        private List<MenuEntry> currentEntries;
        private SoftwareGroup currentSoftwareGroup;
        private string selectedSoftwareKey;
        private readonly HashSet<string> selectedSoftwareEntryIds;
        private readonly Stack<NavigationState> backNavigationHistory;
        private readonly Stack<NavigationState> forwardNavigationHistory;
        private int renderedEntryCount;
        private bool resettingEntries;
        private bool appendingEntries;
        private readonly DispatcherTimer searchDebounce;
        private TextBlock loadHint;
        private TextBlock softwareSelectionText;
        private Button softwareCloseSelectedButton;

        private Grid pageRoot;
        private StackPanel itemsPanel;
        private ScrollViewer listScroll;
        private TextBox searchBox;
        private TextBlock categoryTitle;
        private TextBlock categorySubtitle;
        private TextBlock scannedValue;
        private TextBlock disabledValue;
        private TextBlock guardValue;
        private TextBlock statusText;
        private Border statusPill;
        private Border scanMascot;
        private Button guardButton;
        private Button scanButton;
        private Button reportButton;

        public MainWindow()
        {
            policyStore = new PolicyStore();
            scanner = new MenuScanner();
            enforcement = new EnforcementService(policyStore);
            labService = new ContextMenuLabService();
            selectedLabSample = labService.Samples[0];
            navButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
            navCounts = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
            allEntries = new List<MenuEntry>();
            currentEntries = new List<MenuEntry>();
            selectedSoftwareKey = "";
            selectedSoftwareEntryIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            backNavigationHistory = new Stack<NavigationState>();
            forwardNavigationHistory = new Stack<NavigationState>();
            selectedCategory = "";
            searchDebounce = new DispatcherTimer();
            searchDebounce.Interval = TimeSpan.FromMilliseconds(180);
            searchDebounce.Tick += delegate
            {
                searchDebounce.Stop();
                RenderEntries();
            };

            Title = "右键小守卫";
            Width = 1180;
            Height = 780;
            MinWidth = 980;
            MinHeight = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(Page);
            FontFamily = new FontFamily("Microsoft YaHei UI");
            Foreground = new SolidColorBrush(Ink);
            PreviewMouseDown += OnPreviewMouseButtonDown;

            BuildUi();
            SourceInitialized += delegate { NativeMethods.ApplyRoundedCorners(this); };
            Loaded += OnLoaded;
        }

        private void BuildUi()
        {
            Border shell = new Border();
            shell.Background = new SolidColorBrush(Page);
            shell.BorderBrush = new SolidColorBrush(Color.FromRgb(225, 226, 238));
            shell.BorderThickness = new Thickness(1);

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            shell.Child = root;
            Content = shell;

            root.Children.Add(BuildTitleBar());
            pageRoot = new Grid();
            pageRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(214) });
            pageRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(pageRoot, 1);
            root.Children.Add(pageRoot);

            pageRoot.Children.Add(BuildSidebar());
            UIElement content = BuildMainContent();
            Grid.SetColumn(content, 1);
            pageRoot.Children.Add(content);
        }

        private UIElement BuildTitleBar()
        {
            Grid title = new Grid();
            title.Background = new SolidColorBrush(Surface);
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            title.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2)
                {
                    WindowState = WindowState == WindowState.Maximized
                        ? WindowState.Normal : WindowState.Maximized;
                }
                else { DragMove(); }
            };

            StackPanel brand = new StackPanel();
            brand.Orientation = Orientation.Horizontal;
            brand.VerticalAlignment = VerticalAlignment.Center;
            brand.Margin = new Thickness(18, 0, 0, 0);
            Border icon = new Border();
            icon.Width = 30;
            icon.Height = 30;
            icon.CornerRadius = new CornerRadius(10);
            icon.Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Accent, 0),
                    new GradientStop(Pink, 1)
                }, 45);
            TextBlock paw = new TextBlock();
            paw.Text = "🐾";
            paw.FontSize = 15;
            paw.HorizontalAlignment = HorizontalAlignment.Center;
            paw.VerticalAlignment = VerticalAlignment.Center;
            icon.Child = paw;
            brand.Children.Add(icon);
            TextBlock name = new TextBlock();
            name.Text = "右键小守卫";
            name.FontWeight = FontWeights.SemiBold;
            name.FontSize = 15;
            name.Margin = new Thickness(10, 0, 0, 0);
            name.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(name);
            TextBlock version = new TextBlock();
            version.Text = "  v1.2.1";
            version.Foreground = new SolidColorBrush(Muted);
            version.FontSize = 11;
            version.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(version);
            title.Children.Add(brand);

            StackPanel controls = new StackPanel();
            controls.Orientation = Orientation.Horizontal;
            Grid.SetColumn(controls, 1);
            controls.Children.Add(TitleButton("—", delegate { WindowState = WindowState.Minimized; }));
            controls.Children.Add(TitleButton("□", delegate
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal : WindowState.Maximized;
            }));
            controls.Children.Add(TitleButton("×", delegate { Close(); }));
            title.Children.Add(controls);

            Border line = new Border();
            line.Height = 1;
            line.Background = new SolidColorBrush(Line);
            line.VerticalAlignment = VerticalAlignment.Bottom;
            title.Children.Add(line);
            return title;
        }

        private Button TitleButton(string text, RoutedEventHandler click)
        {
            Button button = RoundedButton(text, Colors.Transparent, Ink, 0);
            button.Width = 48;
            button.Height = 50;
            button.FontSize = 16;
            button.Click += click;
            return button;
        }

        private UIElement BuildSidebar()
        {
            Border sidebar = new Border();
            sidebar.Background = new SolidColorBrush(Surface);
            sidebar.BorderBrush = new SolidColorBrush(Line);
            sidebar.BorderThickness = new Thickness(0, 0, 1, 0);

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sidebar.Child = grid;

            Border helper = new Border();
            helper.Margin = new Thickness(12, 14, 12, 8);
            helper.Padding = new Thickness(12);
            helper.CornerRadius = new CornerRadius(17);
            helper.Background = new SolidColorBrush(Color.FromRgb(244, 243, 255));
            StackPanel helperContent = new StackPanel();
            TextBlock helperTitle = new TextBlock();
            helperTitle.Text = "ฅ  菜单分类";
            helperTitle.FontWeight = FontWeights.SemiBold;
            helperTitle.FontSize = 14;
            helperContent.Children.Add(helperTitle);
            TextBlock helperText = new TextBlock();
            helperText.Text = "传统、扩展、现代菜单都在这里";
            helperText.Foreground = new SolidColorBrush(Muted);
            helperText.FontSize = 10.5;
            helperText.Margin = new Thickness(0, 4, 0, 0);
            helperText.TextWrapping = TextWrapping.Wrap;
            helperContent.Children.Add(helperText);
            helper.Child = helperContent;
            grid.Children.Add(helper);

            ScrollViewer scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Grid.SetRow(scroll, 1);
            StackPanel nav = new StackPanel();
            nav.Margin = new Thickness(9, 3, 9, 10);
            scroll.Content = nav;
            AddNavButton(nav, "", "✦", "全部项目");
            AddNavButton(nav, CategoryNames.Software, "▦", CategoryNames.Software);
            AddNavButton(nav, CategoryNames.Lab, "🧪", CategoryNames.Lab);
            navCounts[CategoryNames.Lab].Text = "TEST";
            AddNavSeparator(nav);
            AddNavButton(nav, CategoryNames.File, "📄", CategoryNames.File);
            AddNavButton(nav, CategoryNames.Folder, "📁", CategoryNames.Folder);
            AddNavButton(nav, CategoryNames.Directory, "🗂", CategoryNames.Directory);
            AddNavButton(nav, CategoryNames.DirectoryBackground, "◫", CategoryNames.DirectoryBackground);
            AddNavButton(nav, CategoryNames.DesktopBackground, "🖥", CategoryNames.DesktopBackground);
            AddNavButton(nav, CategoryNames.Drive, "💽", CategoryNames.Drive);
            AddNavButton(nav, CategoryNames.AllObjects, "◎", CategoryNames.AllObjects);
            AddNavButton(nav, CategoryNames.ThisPc, "💻", CategoryNames.ThisPc);
            AddNavButton(nav, CategoryNames.RecycleBin, "♻", CategoryNames.RecycleBin);
            AddNavButton(nav, CategoryNames.Library, "▤", CategoryNames.Library);
            AddNavSeparator(nav);
            AddNavButton(nav, CategoryNames.ImageMedia, "🖼", CategoryNames.ImageMedia);
            AddNavButton(nav, CategoryNames.ModernApps, "▦", CategoryNames.ModernApps);
            AddNavButton(nav, CategoryNames.NewMenu, "＋", CategoryNames.NewMenu);
            AddNavButton(nav, CategoryNames.SendTo, "➤", CategoryNames.SendTo);
            AddNavButton(nav, CategoryNames.OpenWith, "↗", CategoryNames.OpenWith);
            AddNavButton(nav, CategoryNames.WinX, "⊞", CategoryNames.WinX);
            AddNavButton(nav, CategoryNames.CommandStore, "⚙", CategoryNames.CommandStore);
            grid.Children.Add(scroll);

            Border guardCard = new Border();
            guardCard.Margin = new Thickness(12, 6, 12, 14);
            guardCard.Padding = new Thickness(12);
            guardCard.CornerRadius = new CornerRadius(16);
            guardCard.Background = new SolidColorBrush(Color.FromRgb(239, 251, 247));
            Grid.SetRow(guardCard, 2);
            Grid guardGrid = new Grid();
            guardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            guardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Ellipse dot = new Ellipse();
            dot.Width = 9;
            dot.Height = 9;
            dot.Fill = new SolidColorBrush(Mint);
            dot.Margin = new Thickness(1, 2, 9, 0);
            dot.VerticalAlignment = VerticalAlignment.Top;
            guardGrid.Children.Add(dot);
            StackPanel guardWords = new StackPanel();
            Grid.SetColumn(guardWords, 1);
            TextBlock guardTitle = new TextBlock();
            guardTitle.Text = "强制守护";
            guardTitle.FontWeight = FontWeights.SemiBold;
            guardTitle.FontSize = 12;
            guardWords.Children.Add(guardTitle);
            TextBlock guardHint = new TextBlock();
            guardHint.Text = "被软件写回时自动压制";
            guardHint.Foreground = new SolidColorBrush(Color.FromRgb(73, 141, 120));
            guardHint.FontSize = 9.5;
            guardHint.Margin = new Thickness(0, 3, 0, 0);
            guardWords.Children.Add(guardHint);
            guardGrid.Children.Add(guardWords);
            guardCard.Child = guardGrid;
            grid.Children.Add(guardCard);
            return sidebar;
        }

        private void AddNavSeparator(Panel panel)
        {
            Border line = new Border();
            line.Height = 1;
            line.Background = new SolidColorBrush(Line);
            line.Margin = new Thickness(8, 7, 8, 7);
            panel.Children.Add(line);
        }

        private void AddNavButton(Panel panel, string category, string icon, string label)
        {
            Button button = RoundedButton("", Colors.Transparent, Ink, 12);
            button.Height = 39;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Margin = new Thickness(0, 1, 0, 1);
            button.Tag = category;
            Grid content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(31) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock iconText = new TextBlock();
            iconText.Text = icon;
            iconText.FontSize = 14;
            iconText.VerticalAlignment = VerticalAlignment.Center;
            iconText.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(iconText);
            TextBlock labelText = new TextBlock();
            labelText.Text = label;
            labelText.FontSize = 12.5;
            labelText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(labelText, 1);
            content.Children.Add(labelText);
            TextBlock count = new TextBlock();
            count.Text = "0";
            count.Foreground = new SolidColorBrush(Muted);
            count.FontSize = 10;
            count.VerticalAlignment = VerticalAlignment.Center;
            count.Margin = new Thickness(4, 0, 8, 0);
            Grid.SetColumn(count, 2);
            content.Children.Add(count);
            button.Content = content;
            button.Click += delegate
            {
                string nextCategory = Convert.ToString(button.Tag);
                if (string.Equals(selectedCategory, nextCategory,
                    StringComparison.OrdinalIgnoreCase) &&
                    itemsPanel != null && itemsPanel.Children.Count > 0)
                {
                    if (string.Equals(nextCategory, CategoryNames.Software,
                        StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(selectedSoftwareKey))
                    {
                        NavigateTo(CategoryNames.Software, "");
                        return;
                    }
                    listScroll.ScrollToTop();
                    return;
                }
                NavigateTo(nextCategory, "");
            };
            panel.Children.Add(button);
            navButtons[category] = button;
            navCounts[category] = count;
        }

        private UIElement BuildMainContent()
        {
            Grid main = new Grid();
            main.Margin = new Thickness(22, 18, 22, 14);
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(138) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            main.Children.Add(BuildHero());
            UIElement toolbar = BuildToolbar();
            Grid.SetRow(toolbar, 1);
            main.Children.Add(toolbar);
            listScroll = new ScrollViewer();
            listScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            listScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            listScroll.ScrollChanged += OnListScrollChanged;
            itemsPanel = new StackPanel();
            itemsPanel.Margin = new Thickness(1, 5, 6, 12);
            listScroll.Content = itemsPanel;
            Grid.SetRow(listScroll, 2);
            main.Children.Add(listScroll);
            UIElement status = BuildStatusBar();
            Grid.SetRow(status, 3);
            main.Children.Add(status);
            return main;
        }

        private UIElement BuildHero()
        {
            Border hero = new Border();
            hero.CornerRadius = new CornerRadius(24);
            hero.Padding = new Thickness(24, 19, 22, 18);
            LinearGradientBrush gradient = new LinearGradientBrush();
            gradient.StartPoint = new Point(0, 0);
            gradient.EndPoint = new Point(1, 1);
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(105, 101, 240), 0));
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(146, 126, 255), 0.58));
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 172, 201), 1));
            hero.Background = gradient;

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            hero.Child = grid;

            StackPanel words = new StackPanel();
            categoryTitle = new TextBlock();
            categoryTitle.Text = "全部右键菜单";
            categoryTitle.Foreground = Brushes.White;
            categoryTitle.FontSize = 24;
            categoryTitle.FontWeight = FontWeights.Bold;
            words.Children.Add(categoryTitle);
            categorySubtitle = new TextBlock();
            categorySubtitle.Text = "扫描传统菜单、动态扩展和现代应用，想留谁就留谁";
            categorySubtitle.Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
            categorySubtitle.FontSize = 11.5;
            categorySubtitle.Margin = new Thickness(0, 6, 0, 13);
            words.Children.Add(categorySubtitle);
            StackPanel chips = new StackPanel();
            chips.Orientation = Orientation.Horizontal;
            chips.Children.Add(HeroChip("扫描", out scannedValue));
            chips.Children.Add(HeroChip("已关闭", out disabledValue));
            chips.Children.Add(HeroChip("守护", out guardValue));
            words.Children.Add(chips);
            grid.Children.Add(words);

            Grid mascotArea = new Grid();
            Grid.SetColumn(mascotArea, 1);
            StackPanel mascotRow = new StackPanel();
            mascotRow.Orientation = Orientation.Horizontal;
            mascotRow.HorizontalAlignment = HorizontalAlignment.Right;
            mascotRow.VerticalAlignment = VerticalAlignment.Center;

            StackPanel safetyText = new StackPanel();
            safetyText.VerticalAlignment = VerticalAlignment.Center;
            safetyText.Margin = new Thickness(0, 0, 15, 0);
            TextBlock safeTitle = new TextBlock();
            safeTitle.Text = "回写也不怕";
            safeTitle.Foreground = Brushes.White;
            safeTitle.FontWeight = FontWeights.SemiBold;
            safeTitle.FontSize = 13;
            safetyText.Children.Add(safeTitle);
            TextBlock safeSub = new TextBlock();
            safeSub.Text = "每 1.5 秒核验规则";
            safeSub.Foreground = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255));
            safeSub.FontSize = 10;
            safeSub.Margin = new Thickness(0, 3, 0, 0);
            safetyText.Children.Add(safeSub);
            mascotRow.Children.Add(safetyText);

            scanMascot = new Border();
            scanMascot.Width = 82;
            scanMascot.Height = 82;
            scanMascot.CornerRadius = new CornerRadius(29);
            scanMascot.Background = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255));
            scanMascot.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
            scanMascot.BorderThickness = new Thickness(1);
            Grid mascot = new Grid();
            TextBlock cat = new TextBlock();
            cat.Text = "🐱";
            cat.FontSize = 42;
            cat.HorizontalAlignment = HorizontalAlignment.Center;
            cat.VerticalAlignment = VerticalAlignment.Center;
            mascot.Children.Add(cat);
            Border shield = new Border();
            shield.Width = 28;
            shield.Height = 28;
            shield.CornerRadius = new CornerRadius(10);
            shield.Background = Brushes.White;
            shield.HorizontalAlignment = HorizontalAlignment.Right;
            shield.VerticalAlignment = VerticalAlignment.Bottom;
            TextBlock shieldText = new TextBlock();
            shieldText.Text = "✓";
            shieldText.FontSize = 15;
            shieldText.FontWeight = FontWeights.Bold;
            shieldText.Foreground = new SolidColorBrush(Mint);
            shieldText.HorizontalAlignment = HorizontalAlignment.Center;
            shieldText.VerticalAlignment = VerticalAlignment.Center;
            shield.Child = shieldText;
            mascot.Children.Add(shield);
            scanMascot.Child = mascot;
            mascotRow.Children.Add(scanMascot);
            mascotArea.Children.Add(mascotRow);
            grid.Children.Add(mascotArea);
            return hero;
        }

        private Border HeroChip(string label, out TextBlock valueText)
        {
            Border chip = new Border();
            chip.CornerRadius = new CornerRadius(12);
            chip.Background = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255));
            chip.Padding = new Thickness(11, 7, 11, 7);
            chip.Margin = new Thickness(0, 0, 8, 0);
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            TextBlock labelText = new TextBlock();
            labelText.Text = label + " ";
            labelText.Foreground = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255));
            labelText.FontSize = 10;
            row.Children.Add(labelText);
            valueText = new TextBlock();
            valueText.Text = "—";
            valueText.Foreground = Brushes.White;
            valueText.FontWeight = FontWeights.Bold;
            valueText.FontSize = 10.5;
            row.Children.Add(valueText);
            chip.Child = row;
            return chip;
        }

        private UIElement BuildToolbar()
        {
            Grid toolbar = new Grid();
            toolbar.Margin = new Thickness(0, 10, 0, 4);
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border searchBorder = new Border();
            searchBorder.Height = 42;
            searchBorder.CornerRadius = new CornerRadius(15);
            searchBorder.Background = new SolidColorBrush(Surface);
            searchBorder.BorderBrush = new SolidColorBrush(Line);
            searchBorder.BorderThickness = new Thickness(1);
            searchBorder.Padding = new Thickness(13, 0, 13, 0);
            Grid searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock glass = new TextBlock();
            glass.Text = "⌕";
            glass.FontSize = 20;
            glass.Foreground = new SolidColorBrush(Muted);
            glass.VerticalAlignment = VerticalAlignment.Center;
            searchGrid.Children.Add(glass);
            searchBox = new TextBox();
            searchBox.BorderThickness = new Thickness(0);
            searchBox.Background = Brushes.Transparent;
            searchBox.FontSize = 12.5;
            searchBox.VerticalContentAlignment = VerticalAlignment.Center;
            searchBox.Padding = new Thickness(0);
            searchBox.ToolTip = "搜索名称、来源、命令或 CLSID";
            searchBox.TextChanged += delegate
            {
                searchDebounce.Stop();
                searchDebounce.Start();
            };
            Grid.SetColumn(searchBox, 1);
            searchGrid.Children.Add(searchBox);
            searchBorder.Child = searchGrid;
            toolbar.Children.Add(searchBorder);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(actions, 1);
            reportButton = RoundedButton("扫描报告", Color.FromRgb(240, 241, 249), Ink, 14);
            reportButton.Height = 42;
            reportButton.Margin = new Thickness(0, 0, 8, 0);
            reportButton.Click += delegate { ShowReport(); };
            actions.Children.Add(reportButton);
            guardButton = RoundedButton("🛡 守护中", Mint, Colors.White, 14);
            guardButton.Height = 42;
            guardButton.Margin = new Thickness(0, 0, 8, 0);
            guardButton.Click += delegate { ToggleGuard(); };
            actions.Children.Add(guardButton);
            scanButton = RoundedButton("↻  深度扫描", Accent, Colors.White, 14);
            scanButton.Height = 42;
            scanButton.Click += async delegate { await ScanAsync(); };
            actions.Children.Add(scanButton);
            toolbar.Children.Add(actions);
            return toolbar;
        }

        private UIElement BuildStatusBar()
        {
            Grid status = new Grid();
            status.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            status.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            status.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusPill = new Border();
            statusPill.Width = 8;
            statusPill.Height = 8;
            statusPill.CornerRadius = new CornerRadius(4);
            statusPill.Background = new SolidColorBrush(Mint);
            statusPill.VerticalAlignment = VerticalAlignment.Center;
            status.Children.Add(statusPill);
            statusText = new TextBlock();
            statusText.Text = "准备好啦，点击深度扫描开始";
            statusText.Foreground = new SolidColorBrush(Muted);
            statusText.FontSize = 10.5;
            statusText.VerticalAlignment = VerticalAlignment.Center;
            statusText.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(statusText, 1);
            status.Children.Add(statusText);
            TextBlock note = new TextBlock();
            note.Text = "管理员/系统组件改写后，守护会再次恢复规则";
            note.Foreground = new SolidColorBrush(Color.FromRgb(155, 160, 181));
            note.FontSize = 9.5;
            note.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(note, 2);
            status.Children.Add(note);
            return status;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Opacity = 0;
            DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260));
            BeginAnimation(OpacityProperty, fade);
            try
            {
                TaskSchedulerManager.StopOtherVersionGuards();
                PolicyDocument policy = policyStore.Load();
                if (policy.GuardEnabled)
                {
                    TaskSchedulerManager.Install();
                    TaskSchedulerManager.StartGuardNow();
                }
            }
            catch (Exception ex)
            {
                SetStatus("守护自启动设置失败：" + ex.Message, false);
            }
            UpdateGuardUi();
            await ScanAsync();
        }

        private async Task ScanAsync()
        {
            if (scanning) return;
            scanning = true;
            scanButton.IsEnabled = false;
            scanButton.Content = "扫描中…";
            SetStatus("正在翻遍注册表与应用清单，请稍等一下…", true);
            StartMascotAnimation();
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                PolicyDocument policy = policyStore.Load();
                ScanResult result = await Task.Run(delegate { return scanner.Scan(policy); });
                lastScan = result;
                allEntries = result.Entries;
                watch.Stop();
                UpdateCounts();
                RenderEntries();
                SetStatus("扫描完成：发现 " + allEntries.Count + " 项，用时 " +
                          watch.Elapsed.TotalSeconds.ToString("0.0") + " 秒", true);
            }
            catch (Exception ex)
            {
                SetStatus("扫描失败：" + ex.Message, false);
                MessageBox.Show(this, ex.ToString(), "扫描失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                StopMascotAnimation();
                scanning = false;
                scanButton.IsEnabled = true;
                scanButton.Content = "↻  深度扫描";
            }
        }

        private void OnPreviewMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton1)
            {
                e.Handled = true;
                if (NavigateBack())
                    SetStatus("鼠标侧键：已返回“" + CurrentNavigationTitle() + "”", true);
                else
                    SetStatus("已经是最早打开的页面啦", true);
                return;
            }
            if (e.ChangedButton == MouseButton.XButton2)
            {
                e.Handled = true;
                if (NavigateForward())
                    SetStatus("鼠标侧键：已前进到“" + CurrentNavigationTitle() + "”", true);
                else
                    SetStatus("前面暂时没有页面啦", true);
            }
        }

        private void NavigateTo(string category, string softwareKey)
        {
            NavigationState current = CaptureNavigationState();
            NavigationState next = new NavigationState(category,
                string.Equals(category, CategoryNames.Software,
                    StringComparison.OrdinalIgnoreCase) ? softwareKey : "", 0);
            if (SameNavigationPage(current, next))
            {
                if (listScroll != null) listScroll.ScrollToTop();
                return;
            }
            PushNavigationState(backNavigationHistory, current);
            forwardNavigationHistory.Clear();
            ApplyNavigationState(next);
        }

        private bool NavigateBack()
        {
            if (backNavigationHistory.Count == 0) return false;
            NavigationState current = CaptureNavigationState();
            NavigationState previous = backNavigationHistory.Pop();
            PushNavigationState(forwardNavigationHistory, current);
            ApplyNavigationState(previous);
            return true;
        }

        private bool NavigateForward()
        {
            if (forwardNavigationHistory.Count == 0) return false;
            NavigationState current = CaptureNavigationState();
            NavigationState next = forwardNavigationHistory.Pop();
            PushNavigationState(backNavigationHistory, current);
            ApplyNavigationState(next);
            return true;
        }

        private NavigationState CaptureNavigationState()
        {
            return new NavigationState(selectedCategory, selectedSoftwareKey,
                listScroll == null ? 0 : listScroll.VerticalOffset);
        }

        private void ApplyNavigationState(NavigationState state)
        {
            selectedCategory = state == null ? "" : state.Category;
            selectedSoftwareKey = state == null ? "" : state.SoftwareKey;
            selectedSoftwareEntryIds.Clear();
            searchDebounce.Stop();
            UpdateNavSelection();
            RenderEntries();
            double offset = state == null ? 0 : state.VerticalOffset;
            if (offset <= 0 || listScroll == null) return;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (listScroll != null)
                    listScroll.ScrollToVerticalOffset(offset);
            }), DispatcherPriority.ContextIdle);
        }

        private string CurrentNavigationTitle()
        {
            if (categoryTitle != null &&
                !string.IsNullOrWhiteSpace(categoryTitle.Text))
                return categoryTitle.Text;
            return string.IsNullOrWhiteSpace(selectedCategory)
                ? "全部右键菜单" : selectedCategory;
        }

        private static bool SameNavigationPage(NavigationState left,
            NavigationState right)
        {
            if (left == null || right == null) return false;
            return string.Equals(left.Category, right.Category,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.SoftwareKey, right.SoftwareKey,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void PushNavigationState(Stack<NavigationState> history,
            NavigationState state)
        {
            if (history == null || state == null) return;
            history.Push(state);
            if (history.Count <= 64) return;
            NavigationState[] newestFirst = history.ToArray();
            history.Clear();
            for (int index = Math.Min(64, newestFirst.Length) - 1;
                 index >= 0; index--)
                history.Push(newestFirst[index]);
        }

        private void RenderEntries()
        {
            if (itemsPanel == null) return;
            resettingEntries = true;
            try
            {
                itemsPanel.Children.Clear();
                loadHint = null;
                currentEntries = new List<MenuEntry>();
                currentSoftwareGroup = null;
                renderedEntryCount = 0;
                softwareSelectionText = null;
                softwareCloseSelectedButton = null;
                listScroll.ScrollToTop();
                if (string.Equals(selectedCategory, CategoryNames.Lab,
                    StringComparison.OrdinalIgnoreCase))
                {
                    RenderLab();
                    return;
                }
                if (string.Equals(selectedCategory, CategoryNames.Software,
                    StringComparison.OrdinalIgnoreCase))
                {
                    RenderSoftwareZone();
                    return;
                }
                string query = searchBox == null ? "" : (searchBox.Text ?? "").Trim();
                IEnumerable<MenuEntry> filtered = allEntries;
                if (!string.IsNullOrWhiteSpace(selectedCategory))
                    filtered = filtered.Where(entry =>
                        string.Equals(entry.Category, selectedCategory,
                            StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(query))
                {
                    filtered = filtered.Where(entry =>
                        Contains(entry.Name, query) || Contains(entry.Source, query) ||
                        Contains(entry.Details, query) || Contains(entry.Clsid, query) ||
                        Contains(entry.Command, query));
                }
                currentEntries = filtered.ToList();
                categoryTitle.Text = string.IsNullOrWhiteSpace(selectedCategory)
                    ? "全部右键菜单" : selectedCategory;
                if (currentEntries.Count == 0)
                {
                    categorySubtitle.Text = "这里暂时空空的，换个分类或清除搜索试试";
                    itemsPanel.Children.Add(BuildEmptyState());
                    return;
                }
                AppendNextEntryBatch(true);
            }
            finally
            {
                resettingEntries = false;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (listScroll != null) listScroll.ScrollToTop();
                }), DispatcherPriority.Loaded);
            }
        }

        private void OnListScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (resettingEntries || appendingEntries || e.VerticalChange <= 0 ||
                renderedEntryCount >= currentEntries.Count ||
                string.Equals(selectedCategory, CategoryNames.Lab,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(selectedCategory, CategoryNames.Software,
                    StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(selectedSoftwareKey)) return;
            if (listScroll.VerticalOffset + listScroll.ViewportHeight >=
                listScroll.ExtentHeight - 420)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (!resettingEntries && !appendingEntries)
                        AppendNextEntryBatch(false);
                }), DispatcherPriority.Background);
            }
        }

        private void AppendNextEntryBatch(bool firstBatch)
        {
            if (appendingEntries || renderedEntryCount >= currentEntries.Count) return;
            appendingEntries = true;
            try
            {
                if (loadHint != null && itemsPanel.Children.Contains(loadHint))
                    itemsPanel.Children.Remove(loadHint);
                int batchSize = firstBatch ? 48 : 36;
                int end = Math.Min(currentEntries.Count, renderedEntryCount + batchSize);
                for (int index = renderedEntryCount; index < end; index++)
                {
                    bool softwareDetail = string.Equals(selectedCategory,
                        CategoryNames.Software, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(selectedSoftwareKey);
                    Border card = BuildEntryCard(currentEntries[index], softwareDetail);
                    itemsPanel.Children.Add(card);
                    int animationIndex = index - renderedEntryCount;
                    if (firstBatch && animationIndex < 10)
                    {
                        card.Opacity = 0;
                        TranslateTransform transform = new TranslateTransform(0, 6);
                        card.RenderTransform = transform;
                        DoubleAnimation fade = new DoubleAnimation(
                            0, 1, TimeSpan.FromMilliseconds(170));
                        fade.BeginTime = TimeSpan.FromMilliseconds(animationIndex * 10);
                        card.BeginAnimation(OpacityProperty, fade);
                        DoubleAnimation slide = new DoubleAnimation(
                            6, 0, TimeSpan.FromMilliseconds(170));
                        slide.BeginTime = fade.BeginTime;
                        transform.BeginAnimation(TranslateTransform.YProperty, slide);
                    }
                }
                renderedEntryCount = end;
                UpdateIncrementalLoadUi();
            }
            finally { appendingEntries = false; }
        }

        private void UpdateIncrementalLoadUi()
        {
            int remaining = currentEntries.Count - renderedEntryCount;
            if (currentSoftwareGroup != null)
            {
                int protectedCount = currentSoftwareGroup.Entries.Count(entry =>
                    entry.Protected);
                categorySubtitle.Text = currentSoftwareGroup.Name + " 共 " +
                    currentSoftwareGroup.Entries.Count + " 个右键功能，已守护 " +
                    protectedCount + " 个；当前加载 " + renderedEntryCount + " 个";
            }
            else
            {
                categorySubtitle.Text = "共 " + currentEntries.Count + " 项，已加载 " +
                                        renderedEntryCount + " 项；关闭后由后台守卫持续压制";
            }
            if (remaining <= 0)
            {
                loadHint = null;
                return;
            }
            loadHint = new TextBlock();
            loadHint.Text = "↓  继续向下滚动，按需加载剩余 " + remaining + " 项";
            loadHint.Foreground = new SolidColorBrush(Muted);
            loadHint.FontSize = 10.5;
            loadHint.HorizontalAlignment = HorizontalAlignment.Center;
            loadHint.Margin = new Thickness(0, 10, 0, 18);
            itemsPanel.Children.Add(loadHint);
        }

        private void RenderSoftwareZone()
        {
            string query = searchBox == null ? "" : (searchBox.Text ?? "").Trim();
            List<SoftwareGroup> groups = SoftwareCatalog.Build(allEntries);
            if (string.IsNullOrWhiteSpace(selectedSoftwareKey))
            {
                IEnumerable<SoftwareGroup> filtered = groups;
                if (!string.IsNullOrWhiteSpace(query))
                {
                    filtered = filtered.Where(group =>
                        Contains(group.Name, query) ||
                        Contains(group.Abbreviation, query) ||
                        group.Entries.Any(entry =>
                            Contains(FriendlyDisplayName(entry), query) ||
                            Contains(entry.Source, query)));
                }
                List<SoftwareGroup> visible = filtered.ToList();
                categoryTitle.Text = "软件专区";
                categorySubtitle.Text = "按软件整理右键功能；点击图标进入，可整组或选择关闭";
                if (visible.Count == 0)
                {
                    itemsPanel.Children.Add(BuildEmptyState());
                    return;
                }
                WrapPanel cards = new WrapPanel();
                cards.Margin = new Thickness(0, 5, 0, 12);
                foreach (SoftwareGroup group in visible)
                    cards.Children.Add(BuildSoftwareCard(group));
                itemsPanel.Children.Add(cards);
                return;
            }

            currentSoftwareGroup = groups.FirstOrDefault(group =>
                string.Equals(group.Key, selectedSoftwareKey,
                    StringComparison.OrdinalIgnoreCase));
            if (currentSoftwareGroup == null)
            {
                selectedSoftwareKey = "";
                selectedSoftwareEntryIds.Clear();
                RenderSoftwareZone();
                return;
            }

            categoryTitle.Text = currentSoftwareGroup.Name;
            categorySubtitle.Text = "可以勾选多个功能关闭，也可以一键关闭该软件全部";
            itemsPanel.Children.Add(BuildSoftwareDetailHeader(currentSoftwareGroup));
            IEnumerable<MenuEntry> functions = currentSoftwareGroup.Entries;
            if (!string.IsNullOrWhiteSpace(query))
            {
                functions = functions.Where(entry =>
                    Contains(FriendlyDisplayName(entry), query) ||
                    Contains(entry.Source, query) ||
                    Contains(entry.Details, query));
            }
            currentEntries = functions.ToList();
            if (currentEntries.Count == 0)
            {
                itemsPanel.Children.Add(BuildEmptyState());
                return;
            }
            AppendNextEntryBatch(true);
            UpdateSoftwareSelectionUi();
        }

        private Button BuildSoftwareCard(SoftwareGroup group)
        {
            Button card = RoundedButton("", Surface, Ink, 18);
            card.Width = 258;
            card.Height = 112;
            card.Padding = new Thickness(14, 12, 14, 12);
            card.Margin = new Thickness(0, 0, 12, 12);
            card.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            card.VerticalContentAlignment = VerticalAlignment.Stretch;
            card.Tag = group;

            Grid content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            content.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });
            content.Children.Add(BuildSoftwareIcon(group, 56));

            StackPanel words = new StackPanel();
            words.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(words, 1);
            TextBlock name = new TextBlock();
            name.Text = group.Name;
            name.FontSize = 14;
            name.FontWeight = FontWeights.SemiBold;
            name.Foreground = new SolidColorBrush(Ink);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            words.Children.Add(name);
            int protectedCount = group.Entries.Count(entry => entry.Protected);
            TextBlock count = new TextBlock();
            count.Text = group.Entries.Count + " 个功能 · " + protectedCount + " 个已关闭";
            count.Foreground = new SolidColorBrush(Muted);
            count.FontSize = 10;
            count.Margin = new Thickness(0, 6, 0, 0);
            words.Children.Add(count);
            TextBlock enter = new TextBlock();
            enter.Text = "进入管理  ›";
            enter.Foreground = new SolidColorBrush(AccentDark);
            enter.FontSize = 10.5;
            enter.Margin = new Thickness(0, 8, 0, 0);
            words.Children.Add(enter);
            content.Children.Add(words);
            card.Content = content;
            card.Click += delegate
            {
                NavigateTo(CategoryNames.Software, group.Key);
            };
            return card;
        }

        private UIElement BuildSoftwareIcon(SoftwareGroup group, double size)
        {
            Grid holder = new Grid();
            holder.Width = size + 6;
            holder.Height = size + 6;
            holder.HorizontalAlignment = HorizontalAlignment.Left;
            holder.VerticalAlignment = VerticalAlignment.Center;

            Border tile = new Border();
            tile.Width = size;
            tile.Height = size;
            tile.CornerRadius = new CornerRadius(17);
            tile.Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(246, 245, 255), 0),
                    new GradientStop(Color.FromRgb(237, 246, 255), 1)
                }, 45);
            ImageSource icon = group.IconEntry == null
                ? null : IconResolver.Resolve(group.IconEntry);
            if (icon != null)
            {
                Image image = new Image();
                image.Source = icon;
                image.Width = size - 19;
                image.Height = size - 19;
                image.Stretch = Stretch.Uniform;
                tile.Child = image;
            }
            else
            {
                TextBlock letters = new TextBlock();
                letters.Text = group.Abbreviation;
                letters.FontSize = group.Abbreviation.Length > 2 ? 12 : 16;
                letters.FontWeight = FontWeights.Bold;
                letters.Foreground = new SolidColorBrush(AccentDark);
                letters.HorizontalAlignment = HorizontalAlignment.Center;
                letters.VerticalAlignment = VerticalAlignment.Center;
                tile.Child = letters;
            }
            holder.Children.Add(tile);

            Border abbreviation = new Border();
            abbreviation.Background = new SolidColorBrush(Accent);
            abbreviation.CornerRadius = new CornerRadius(7);
            abbreviation.Padding = new Thickness(5, 2, 5, 2);
            abbreviation.HorizontalAlignment = HorizontalAlignment.Right;
            abbreviation.VerticalAlignment = VerticalAlignment.Bottom;
            TextBlock shortName = new TextBlock();
            shortName.Text = group.Abbreviation;
            shortName.Foreground = Brushes.White;
            shortName.FontSize = 7.8;
            shortName.FontWeight = FontWeights.Bold;
            abbreviation.Child = shortName;
            holder.Children.Add(abbreviation);
            return holder;
        }

        private UIElement BuildSoftwareDetailHeader(SoftwareGroup group)
        {
            Border header = new Border();
            header.Background = new SolidColorBrush(Color.FromRgb(244, 243, 255));
            header.CornerRadius = new CornerRadius(20);
            header.Padding = new Thickness(17, 14, 17, 14);
            header.Margin = new Thickness(0, 5, 0, 12);

            Grid root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            root.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(BuildSoftwareIcon(group, 56));

            StackPanel content = new StackPanel();
            Grid.SetColumn(content, 1);
            Grid top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel title = new StackPanel();
            TextBlock name = new TextBlock();
            name.Text = group.Name + " · " + group.Abbreviation;
            name.FontSize = 15;
            name.FontWeight = FontWeights.SemiBold;
            title.Children.Add(name);
            softwareSelectionText = new TextBlock();
            softwareSelectionText.Foreground = new SolidColorBrush(Muted);
            softwareSelectionText.FontSize = 10;
            softwareSelectionText.Margin = new Thickness(0, 4, 0, 0);
            title.Children.Add(softwareSelectionText);
            top.Children.Add(title);
            Button back = RoundedButton("← 返回软件列表",
                Color.FromRgb(232, 231, 248), AccentDark, 11);
            back.Height = 32;
            back.Click += delegate
            {
                NavigateTo(CategoryNames.Software, "");
            };
            Grid.SetColumn(back, 1);
            top.Children.Add(back);
            content.Children.Add(top);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.Margin = new Thickness(0, 11, 0, 0);
            Button selectAll = RoundedButton("☑ 全选可关闭",
                Color.FromRgb(232, 231, 248), AccentDark, 11);
            selectAll.Height = 32;
            selectAll.Click += delegate
            {
                foreach (MenuEntry entry in group.Entries.Where(item => !item.Protected))
                    selectedSoftwareEntryIds.Add(entry.Id);
                RenderEntries();
            };
            actions.Children.Add(selectAll);
            Button clear = RoundedButton("清空选择",
                Color.FromRgb(238, 239, 246), Muted, 11);
            clear.Height = 32;
            clear.Margin = new Thickness(8, 0, 0, 0);
            clear.Click += delegate
            {
                selectedSoftwareEntryIds.Clear();
                RenderEntries();
            };
            actions.Children.Add(clear);
            softwareCloseSelectedButton = RoundedButton("关闭已选",
                Color.FromRgb(255, 236, 244), Color.FromRgb(198, 74, 122), 11);
            softwareCloseSelectedButton.Height = 32;
            softwareCloseSelectedButton.Margin = new Thickness(8, 0, 0, 0);
            softwareCloseSelectedButton.Click += async delegate
            {
                List<MenuEntry> selected = group.Entries.Where(entry =>
                    selectedSoftwareEntryIds.Contains(entry.Id)).ToList();
                await CloseSoftwareEntriesAsync(group, selected,
                    softwareCloseSelectedButton, "已选功能");
            };
            actions.Children.Add(softwareCloseSelectedButton);
            Button closeAll = RoundedButton("一键关闭全部", Accent, Colors.White, 11);
            closeAll.Height = 32;
            closeAll.Margin = new Thickness(8, 0, 0, 0);
            closeAll.Click += async delegate
            {
                await CloseSoftwareEntriesAsync(group, group.Entries,
                    closeAll, group.Name + " 全部功能");
            };
            actions.Children.Add(closeAll);
            content.Children.Add(actions);
            root.Children.Add(content);
            header.Child = root;
            return header;
        }

        private void UpdateSoftwareSelectionUi()
        {
            if (softwareSelectionText == null) return;
            int selected = currentSoftwareGroup == null ? 0 :
                currentSoftwareGroup.Entries.Count(entry =>
                    selectedSoftwareEntryIds.Contains(entry.Id) && !entry.Protected);
            int protectedCount = currentSoftwareGroup == null ? 0 :
                currentSoftwareGroup.Entries.Count(entry => entry.Protected);
            softwareSelectionText.Text = "已选 " + selected + " 个 · 已守护 " +
                                         protectedCount + " 个";
            if (softwareCloseSelectedButton != null)
                softwareCloseSelectedButton.IsEnabled = selected > 0;
        }

        private async Task CloseSoftwareEntriesAsync(SoftwareGroup group,
            IEnumerable<MenuEntry> requested, Button button, string operationName)
        {
            List<MenuEntry> targets = (requested ?? Enumerable.Empty<MenuEntry>())
                .Where(entry => !entry.Protected)
                .GroupBy(SoftwareCatalog.ControlKey, StringComparer.OrdinalIgnoreCase)
                .Select(items => items.First()).ToList();
            if (targets.Count == 0)
            {
                SetStatus("没有需要关闭的项目", true);
                UpdateSoftwareSelectionUi();
                return;
            }

            int critical = targets.Count(entry => entry.IsCritical && entry.Enabled);
            if (critical > 0)
            {
                MessageBoxResult answer = MessageBox.Show(this,
                    "所选项目中有 " + critical + " 个系统核心入口。\n\n" +
                    "批量关闭可能影响常用操作，仍要继续吗？",
                    "确认批量关闭", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) return;
            }

            button.IsEnabled = false;
            string oldText = Convert.ToString(button.Content);
            button.Content = "正在关闭 " + targets.Count + " 项…";
            List<MenuEntry> completed = new List<MenuEntry>();
            List<string> errors = new List<string>();
            await Task.Run(delegate
            {
                foreach (MenuEntry entry in targets)
                {
                    try
                    {
                        if (entry.Enabled) enforcement.Disable(entry);
                        else enforcement.AdoptDisabled(entry);
                        completed.Add(entry);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(FriendlyDisplayName(entry) + "：" + ex.Message);
                    }
                }
            });

            foreach (MenuEntry entry in completed)
            {
                entry.Enabled = false;
                entry.Protected = true;
                selectedSoftwareEntryIds.Remove(entry.Id);
            }
            try
            {
                TaskSchedulerManager.Install();
                TaskSchedulerManager.StartGuardNow();
            }
            catch { }

            UpdateCounts();
            if (errors.Count == 0)
            {
                SetStatus(operationName + "已关闭 " + completed.Count +
                          " 项，强制守护已接管", true);
            }
            else
            {
                SetStatus("已关闭 " + completed.Count + " 项，" +
                          errors.Count + " 项失败", false);
                MessageBox.Show(this, string.Join("\n", errors.Take(12).ToArray()),
                    "部分项目没有完成", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            button.Content = oldText;
            button.IsEnabled = true;
            RenderEntries();
        }

        private void RenderLab()
        {
            categoryTitle.Text = "右键实验室";
            categorySubtitle.Text = "直接读取 Windows 为不同类型对象生成的实际右键菜单";
            itemsPanel.Children.Add(BuildLabSelector());
            try
            {
                List<string> verbs = labService.GetNativeVerbNames(selectedLabSample);
                itemsPanel.Children.Add(BuildNativeMenuPreview(verbs));
                SetStatus("实验室已读取“" + selectedLabSample.Label + "”的 " +
                          verbs.Count(value => !string.IsNullOrWhiteSpace(value)) + " 个菜单动作", true);
            }
            catch (Exception ex)
            {
                Border error = new Border();
                error.CornerRadius = new CornerRadius(18);
                error.Background = new SolidColorBrush(Color.FromRgb(255, 241, 246));
                error.Padding = new Thickness(18);
                TextBlock words = new TextBlock();
                words.Text = "这个类型暂时无法读取：" + ex.Message;
                words.Foreground = new SolidColorBrush(Color.FromRgb(190, 78, 119));
                words.TextWrapping = TextWrapping.Wrap;
                error.Child = words;
                itemsPanel.Children.Add(error);
            }
        }

        private UIElement BuildLabSelector()
        {
            Border card = new Border();
            card.Background = new SolidColorBrush(Surface);
            card.BorderBrush = new SolidColorBrush(Line);
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(18);
            card.Padding = new Thickness(14);
            card.Margin = new Thickness(0, 0, 0, 10);
            StackPanel content = new StackPanel();
            TextBlock title = new TextBlock();
            title.Text = "选择要模拟的对象";
            title.FontSize = 12.5;
            title.FontWeight = FontWeights.SemiBold;
            content.Children.Add(title);
            WrapPanel samples = new WrapPanel();
            samples.Margin = new Thickness(0, 10, 0, 5);
            foreach (LabSample sample in labService.Samples)
            {
                bool selected = string.Equals(sample.Extension, selectedLabSample.Extension,
                    StringComparison.OrdinalIgnoreCase) && sample.IsFolder == selectedLabSample.IsFolder;
                Button button = RoundedButton(sample.Icon + "  " + sample.Label,
                    selected ? Accent : Color.FromRgb(243, 244, 250),
                    selected ? Colors.White : Ink, 12);
                button.Height = 36;
                button.Margin = new Thickness(0, 0, 7, 7);
                button.Tag = sample;
                button.Click += delegate
                {
                    selectedLabSample = (LabSample)button.Tag;
                    RenderEntries();
                };
                samples.Children.Add(button);
            }
            content.Children.Add(samples);

            Grid custom = new Grid();
            custom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            custom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border inputBorder = new Border();
            inputBorder.Height = 36;
            inputBorder.CornerRadius = new CornerRadius(12);
            inputBorder.Background = new SolidColorBrush(Color.FromRgb(248, 248, 252));
            inputBorder.BorderBrush = new SolidColorBrush(Line);
            inputBorder.BorderThickness = new Thickness(1);
            TextBox extensionBox = new TextBox();
            extensionBox.Text = ".psd";
            extensionBox.BorderThickness = new Thickness(0);
            extensionBox.Background = Brushes.Transparent;
            extensionBox.Padding = new Thickness(11, 0, 11, 0);
            extensionBox.VerticalContentAlignment = VerticalAlignment.Center;
            extensionBox.ToolTip = "输入任意扩展名，例如 .psd、.mkv、.7z";
            inputBorder.Child = extensionBox;
            custom.Children.Add(inputBorder);
            Button customButton = RoundedButton("🧩  测试自定义扩展名",
                Color.FromRgb(238, 237, 255), AccentDark, 12);
            customButton.Height = 36;
            customButton.Margin = new Thickness(8, 0, 0, 0);
            customButton.Click += delegate
            {
                selectedLabSample = labService.CreateCustom(extensionBox.Text);
                RenderEntries();
            };
            Grid.SetColumn(customButton, 1);
            custom.Children.Add(customButton);
            content.Children.Add(custom);
            card.Child = content;
            return card;
        }

        private UIElement BuildNativeMenuPreview(List<string> verbs)
        {
            Grid layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.Margin = new Thickness(0, 0, 0, 16);

            Border previewCard = new Border();
            previewCard.Background = new SolidColorBrush(Color.FromRgb(245, 246, 251));
            previewCard.BorderBrush = new SolidColorBrush(Line);
            previewCard.BorderThickness = new Thickness(1);
            previewCard.CornerRadius = new CornerRadius(20);
            previewCard.Padding = new Thickness(18);
            StackPanel previewContent = new StackPanel();
            TextBlock previewTitle = new TextBlock();
            previewTitle.Text = "Windows 实际菜单预览";
            previewTitle.FontSize = 12.5;
            previewTitle.FontWeight = FontWeights.SemiBold;
            previewTitle.Margin = new Thickness(0, 0, 0, 10);
            previewContent.Children.Add(previewTitle);

            Border nativeMenu = new Border();
            nativeMenu.Background = Brushes.White;
            nativeMenu.BorderBrush = new SolidColorBrush(Color.FromRgb(215, 216, 224));
            nativeMenu.BorderThickness = new Thickness(1);
            nativeMenu.CornerRadius = new CornerRadius(7);
            nativeMenu.Padding = new Thickness(5, 6, 5, 6);
            nativeMenu.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 16,
                ShadowDepth = 3,
                Opacity = 0.16
            };
            StackPanel menuRows = new StackPanel();
            foreach (string verb in verbs)
            {
                if (string.IsNullOrWhiteSpace(verb))
                {
                    Border separator = new Border();
                    separator.Height = 1;
                    separator.Background = new SolidColorBrush(Color.FromRgb(232, 232, 236));
                    separator.Margin = new Thickness(6, 5, 6, 5);
                    menuRows.Children.Add(separator);
                    continue;
                }
                Grid menuRow = new Grid();
                menuRow.Height = 31;
                menuRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(29) });
                menuRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                menuRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                string glyph = VerbGlyph(verb);
                if (!string.IsNullOrWhiteSpace(glyph))
                {
                    TextBlock icon = new TextBlock();
                    icon.Text = glyph;
                    icon.FontSize = 14;
                    icon.HorizontalAlignment = HorizontalAlignment.Center;
                    icon.VerticalAlignment = VerticalAlignment.Center;
                    menuRow.Children.Add(icon);
                }
                TextBlock label = new TextBlock();
                label.Text = verb;
                label.FontFamily = new FontFamily("Microsoft YaHei UI");
                label.FontSize = 11.5;
                label.Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 35));
                label.VerticalAlignment = VerticalAlignment.Center;
                label.TextTrimming = TextTrimming.CharacterEllipsis;
                Grid.SetColumn(label, 1);
                menuRow.Children.Add(label);
                if (ContextMenuLabService.LooksLikeSubmenu(verb))
                {
                    TextBlock arrow = new TextBlock();
                    arrow.Text = "›";
                    arrow.FontSize = 18;
                    arrow.VerticalAlignment = VerticalAlignment.Center;
                    arrow.HorizontalAlignment = HorizontalAlignment.Center;
                    Grid.SetColumn(arrow, 2);
                    menuRow.Children.Add(arrow);
                }
                menuRows.Children.Add(menuRow);
            }
            nativeMenu.Child = menuRows;
            previewContent.Children.Add(nativeMenu);
            previewCard.Child = previewContent;
            layout.Children.Add(previewCard);

            Border infoCard = new Border();
            infoCard.Background = new SolidColorBrush(Surface);
            infoCard.BorderBrush = new SolidColorBrush(Line);
            infoCard.BorderThickness = new Thickness(1);
            infoCard.CornerRadius = new CornerRadius(20);
            infoCard.Padding = new Thickness(23);
            infoCard.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(infoCard, 1);
            StackPanel info = new StackPanel();
            Border sampleIcon = new Border();
            sampleIcon.Width = 72;
            sampleIcon.Height = 72;
            sampleIcon.CornerRadius = new CornerRadius(24);
            sampleIcon.Background = new LinearGradientBrush(
                Color.FromRgb(239, 238, 255), Color.FromRgb(255, 235, 244), 45);
            sampleIcon.HorizontalAlignment = HorizontalAlignment.Left;
            TextBlock sampleGlyph = new TextBlock();
            sampleGlyph.Text = selectedLabSample.Icon;
            sampleGlyph.FontSize = 34;
            sampleGlyph.HorizontalAlignment = HorizontalAlignment.Center;
            sampleGlyph.VerticalAlignment = VerticalAlignment.Center;
            sampleIcon.Child = sampleGlyph;
            info.Children.Add(sampleIcon);
            TextBlock infoTitle = new TextBlock();
            infoTitle.Text = selectedLabSample.Label;
            infoTitle.FontSize = 19;
            infoTitle.FontWeight = FontWeights.Bold;
            infoTitle.Margin = new Thickness(0, 15, 0, 0);
            info.Children.Add(infoTitle);
            TextBlock extension = new TextBlock();
            extension.Text = selectedLabSample.IsFolder ? "对象类型：文件夹" :
                "扩展名：" + selectedLabSample.Extension.ToUpperInvariant();
            extension.Foreground = new SolidColorBrush(Muted);
            extension.FontSize = 11;
            extension.Margin = new Thickness(0, 5, 0, 0);
            info.Children.Add(extension);
            TextBlock explanation = new TextBlock();
            explanation.Text = "这里的文字和顺序由 Windows 当前真实返回。动态子菜单会用箭头标记；点击下方按钮可到资源管理器做最终实测。";
            explanation.Foreground = new SolidColorBrush(Muted);
            explanation.FontSize = 10.5;
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.LineHeight = 19;
            explanation.Margin = new Thickness(0, 17, 0, 18);
            info.Children.Add(explanation);
            Button explorerButton = RoundedButton("在资源管理器中真实右键测试",
                Accent, Colors.White, 14);
            explorerButton.Height = 42;
            explorerButton.HorizontalAlignment = HorizontalAlignment.Left;
            explorerButton.Click += delegate
            {
                try
                {
                    labService.OpenInExplorer(selectedLabSample);
                    SetStatus("测试对象已在资源管理器中选中，请直接右键查看", true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "无法打开测试位置",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            info.Children.Add(explorerButton);
            TextBlock privacy = new TextBlock();
            privacy.Text = "测试文件为空白样本，仅保存在本机实验室目录。";
            privacy.Foreground = new SolidColorBrush(Color.FromRgb(157, 161, 181));
            privacy.FontSize = 9;
            privacy.Margin = new Thickness(0, 10, 0, 0);
            info.Children.Add(privacy);
            infoCard.Child = info;
            layout.Children.Add(infoCard);
            return layout;
        }

        private static string VerbGlyph(string verb)
        {
            if (verb.IndexOf("照片", StringComparison.CurrentCultureIgnoreCase) >= 0) return "🖼";
            if (verb.IndexOf("画图", StringComparison.CurrentCultureIgnoreCase) >= 0) return "🎨";
            if (verb.IndexOf("Clipchamp", StringComparison.OrdinalIgnoreCase) >= 0) return "🎬";
            if (verb.IndexOf("Defender", StringComparison.OrdinalIgnoreCase) >= 0) return "🛡";
            if (verb.IndexOf("OneDrive", StringComparison.OrdinalIgnoreCase) >= 0) return "☁";
            if (verb.IndexOf("记事本", StringComparison.CurrentCultureIgnoreCase) >= 0) return "📝";
            if (verb.IndexOf("打印", StringComparison.CurrentCultureIgnoreCase) >= 0) return "🖨";
            return "";
        }

        private Border BuildEntryCard(MenuEntry entry)
        {
            return BuildEntryCard(entry, false);
        }

        private Border BuildEntryCard(MenuEntry entry, bool selectable)
        {
            Border card = new Border();
            card.Background = new SolidColorBrush(Surface);
            card.BorderBrush = new SolidColorBrush(Line);
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(17);
            card.Padding = new Thickness(15, 12, 14, 12);
            card.Margin = new Thickness(0, 0, 0, 9);
            card.ToolTip = entry.Details;
            card.Tag = entry;

            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            row.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(selectable ? 34 : 0) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(53) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            card.Child = row;

            Border accent = new Border();
            accent.Width = 4;
            accent.CornerRadius = new CornerRadius(2);
            accent.Background = new SolidColorBrush(entry.Protected ? Pink :
                entry.Enabled ? Mint : Color.FromRgb(189, 192, 208));
            accent.Margin = new Thickness(0, 1, 0, 1);
            row.Children.Add(accent);

            if (selectable)
            {
                if (entry.Protected) selectedSoftwareEntryIds.Remove(entry.Id);
                CheckBox selection = new CheckBox();
                selection.IsChecked = selectedSoftwareEntryIds.Contains(entry.Id);
                selection.IsEnabled = !entry.Protected;
                selection.VerticalAlignment = VerticalAlignment.Center;
                selection.HorizontalAlignment = HorizontalAlignment.Center;
                selection.ToolTip = entry.Protected ? "已经由守护关闭" : "选择后可批量关闭";
                selection.Click += delegate
                {
                    if (selection.IsChecked == true)
                        selectedSoftwareEntryIds.Add(entry.Id);
                    else selectedSoftwareEntryIds.Remove(entry.Id);
                    UpdateSoftwareSelectionUi();
                };
                Grid.SetColumn(selection, 1);
                row.Children.Add(selection);
            }

            Border iconTile = new Border();
            iconTile.Width = 40;
            iconTile.Height = 40;
            iconTile.CornerRadius = new CornerRadius(13);
            iconTile.Background = new SolidColorBrush(Color.FromRgb(245, 245, 252));
            iconTile.VerticalAlignment = VerticalAlignment.Center;
            iconTile.HorizontalAlignment = HorizontalAlignment.Center;
            ImageSource resolvedIcon = IconResolver.Resolve(entry);
            if (resolvedIcon != null)
            {
                Image iconImage = new Image();
                iconImage.Source = resolvedIcon;
                iconImage.Width = 25;
                iconImage.Height = 25;
                iconImage.Stretch = Stretch.Uniform;
                iconTile.Child = iconImage;
            }
            else
            {
                TextBlock fallbackIcon = new TextBlock();
                fallbackIcon.Text = CategoryIcon(entry.Category);
                fallbackIcon.FontSize = 18;
                fallbackIcon.HorizontalAlignment = HorizontalAlignment.Center;
                fallbackIcon.VerticalAlignment = VerticalAlignment.Center;
                iconTile.Child = fallbackIcon;
            }
            Grid.SetColumn(iconTile, 2);
            row.Children.Add(iconTile);

            StackPanel text = new StackPanel();
            text.Margin = new Thickness(10, 0, 12, 0);
            Grid.SetColumn(text, 3);
            StackPanel titleRow = new StackPanel();
            titleRow.Orientation = Orientation.Horizontal;
            TextBlock name = new TextBlock();
            name.Text = FriendlyDisplayName(entry);
            name.FontSize = 13.2;
            name.FontWeight = FontWeights.SemiBold;
            name.Foreground = new SolidColorBrush(Ink);
            name.VerticalAlignment = VerticalAlignment.Center;
            titleRow.Children.Add(name);
            if (entry.Protected) titleRow.Children.Add(Badge("强制压制", Pink, Colors.White));
            else if (!entry.Enabled) titleRow.Children.Add(Badge("已被系统禁用",
                Color.FromRgb(232, 233, 241), Muted));
            if (entry.IsMicrosoft) titleRow.Children.Add(Badge("Microsoft",
                Color.FromRgb(235, 243, 255), Color.FromRgb(69, 113, 184)));
            if (entry.IsCritical) titleRow.Children.Add(Badge("核心项",
                Color.FromRgb(255, 242, 226), Color.FromRgb(191, 119, 42)));
            text.Children.Add(titleRow);
            TextBlock source = new TextBlock();
            string kindText = KindName(entry.Kind);
            source.Text = entry.Source.IndexOf(kindText, StringComparison.OrdinalIgnoreCase) >= 0
                ? entry.Source : entry.Source + "  ·  " + kindText;
            source.Foreground = new SolidColorBrush(Muted);
            source.FontSize = 10;
            source.Margin = new Thickness(0, 5, 0, 0);
            source.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(source);
            row.Children.Add(text);

            Button toggle = RoundedButton(entry.Protected ? "恢复显示" :
                entry.Enabled ? "关闭并守护" : "纳入守护",
                entry.Protected ? Color.FromRgb(255, 239, 246) :
                entry.Enabled ? Accent : Color.FromRgb(239, 249, 246),
                entry.Protected ? Color.FromRgb(210, 83, 130) :
                entry.Enabled ? Colors.White : Color.FromRgb(42, 145, 114), 13);
            toggle.Height = 36;
            toggle.MinWidth = 100;
            toggle.Tag = entry;
            toggle.VerticalAlignment = VerticalAlignment.Center;
            toggle.Click += async delegate { await ToggleEntryAsync(entry, toggle); };
            Grid.SetColumn(toggle, 4);
            row.Children.Add(toggle);
            return card;
        }

        private async Task ToggleEntryAsync(MenuEntry entry, Button button)
        {
            if (entry.IsCritical && entry.Enabled)
            {
                MessageBoxResult answer = MessageBox.Show(this,
                    "“" + entry.Name + "”被识别为系统核心入口。\n\n关闭它可能影响常用操作，仍要继续吗？",
                    "先确认一下", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) return;
            }
            button.IsEnabled = false;
            string oldText = Convert.ToString(button.Content);
            button.Content = "处理中…";
            try
            {
                bool shouldProtect = !entry.Protected;
                bool wasAlreadyDisabled = shouldProtect && !entry.Enabled;
                await Task.Run(delegate
                {
                    if (wasAlreadyDisabled) enforcement.AdoptDisabled(entry);
                    else if (shouldProtect) enforcement.Disable(entry);
                    else enforcement.Enable(entry);
                });
                if (shouldProtect)
                {
                    entry.Enabled = false;
                    entry.Protected = true;
                    selectedSoftwareEntryIds.Remove(entry.Id);
                    SetStatus("已强制关闭“" + entry.Name + "”，守护正在盯着它", true);
                }
                else
                {
                    entry.Enabled = true;
                    entry.Protected = false;
                    SetStatus("已恢复“" + entry.Name + "”", true);
                }
                try
                {
                    TaskSchedulerManager.Install();
                    TaskSchedulerManager.StartGuardNow();
                }
                catch { }
                UpdateCounts();
                RefreshEntryCard(entry);
            }
            catch (Exception ex)
            {
                button.Content = oldText;
                MessageBox.Show(this, ex.Message, "操作没有完成",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("操作失败：" + ex.Message, false);
            }
            finally { button.IsEnabled = true; }
        }

        private void RefreshEntryCard(MenuEntry entry)
        {
            for (int index = 0; index < itemsPanel.Children.Count; index++)
            {
                Border card = itemsPanel.Children[index] as Border;
                if (card == null || !object.ReferenceEquals(card.Tag, entry)) continue;
                itemsPanel.Children.RemoveAt(index);
                bool softwareDetail = string.Equals(selectedCategory,
                    CategoryNames.Software, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(selectedSoftwareKey);
                itemsPanel.Children.Insert(index, BuildEntryCard(entry, softwareDetail));
                UpdateSoftwareSelectionUi();
                return;
            }
            RenderEntries();
        }

        private UIElement BuildEmptyState()
        {
            Border empty = new Border();
            empty.Background = new SolidColorBrush(Surface);
            empty.BorderBrush = new SolidColorBrush(Line);
            empty.BorderThickness = new Thickness(1);
            empty.CornerRadius = new CornerRadius(22);
            empty.Padding = new Thickness(30);
            empty.Margin = new Thickness(0, 8, 0, 0);
            StackPanel words = new StackPanel();
            TextBlock cat = new TextBlock();
            cat.Text = "ฅ^•ﻌ•^ฅ";
            cat.FontSize = 30;
            cat.HorizontalAlignment = HorizontalAlignment.Center;
            words.Children.Add(cat);
            TextBlock title = new TextBlock();
            title.Text = scanning ? "还在认真翻找中…" : "没有找到匹配项";
            title.FontSize = 14;
            title.FontWeight = FontWeights.SemiBold;
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.Margin = new Thickness(0, 10, 0, 0);
            words.Children.Add(title);
            TextBlock hint = new TextBlock();
            hint.Text = "换个分类或搜索词看看吧";
            hint.Foreground = new SolidColorBrush(Muted);
            hint.FontSize = 10.5;
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.Margin = new Thickness(0, 5, 0, 0);
            words.Children.Add(hint);
            empty.Child = words;
            return empty;
        }

        private void UpdateCounts()
        {
            if (navCounts.ContainsKey(""))
                navCounts[""].Text = allEntries.Count.ToString();
            TextBlock softwareCount;
            if (navCounts.TryGetValue(CategoryNames.Software, out softwareCount))
                softwareCount.Text = SoftwareCatalog.Build(allEntries).Count.ToString();
            foreach (string category in CategoryNames.Ordered)
            {
                TextBlock count;
                if (navCounts.TryGetValue(category, out count))
                    count.Text = allEntries.Count(entry =>
                        string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase)).ToString();
            }
            scannedValue.Text = allEntries.Count.ToString();
            int disabled = allEntries.Count(entry => entry.Protected);
            disabledValue.Text = disabled.ToString();
            PolicyDocument policy = policyStore.Load();
            guardValue.Text = policy.GuardEnabled ? "ON" : "OFF";
            UpdateGuardUi();
        }

        private void UpdateGuardUi()
        {
            PolicyDocument policy = policyStore.Load();
            if (policy.GuardEnabled)
            {
                guardButton.Content = "🛡 守护中";
                guardButton.Background = new SolidColorBrush(Mint);
                guardValue.Text = "ON";
            }
            else
            {
                guardButton.Content = "守护已停";
                guardButton.Background = new SolidColorBrush(Color.FromRgb(183, 187, 203));
                guardValue.Text = "OFF";
            }
        }

        private void ToggleGuard()
        {
            try
            {
                PolicyDocument policy = policyStore.Load();
                bool next = !policy.GuardEnabled;
                enforcement.SetGuardEnabled(next);
                if (next)
                {
                    TaskSchedulerManager.Install();
                    TaskSchedulerManager.StartGuardNow();
                    SetStatus("强制守护已开启：软件写回的菜单会被再次压制", true);
                }
                else
                {
                    TaskSchedulerManager.Uninstall();
                    SetStatus("守护已暂停；现有禁用标记仍然保留", false);
                }
                UpdateGuardUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "守护设置失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowReport()
        {
            if (lastScan == null)
            {
                MessageBox.Show(this, "还没有扫描报告。", "扫描报告",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string warning = lastScan.Warnings.Count == 0
                ? "所有可访问位置均已完成，没有发现读取错误。"
                : string.Join("\n", lastScan.Warnings.Take(18).ToArray());
            MessageBox.Show(this,
                "完成时间：" + lastScan.CompletedAt.ToString("yyyy-MM-dd HH:mm:ss") +
                "\n扫描结果：" + lastScan.Entries.Count + " 项" +
                "\n受保护：" + lastScan.Entries.Count(entry => entry.Protected) + " 项" +
                "\n\n" + warning,
                "深度扫描报告", MessageBoxButton.OK,
                lastScan.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void UpdateNavSelection()
        {
            foreach (KeyValuePair<string, Button> pair in navButtons)
            {
                bool selected = string.Equals(pair.Key, selectedCategory,
                    StringComparison.OrdinalIgnoreCase);
                pair.Value.Background = new SolidColorBrush(selected
                    ? Color.FromRgb(238, 237, 255) : Colors.Transparent);
                pair.Value.Foreground = new SolidColorBrush(selected ? AccentDark : Ink);
                pair.Value.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        private void SetStatus(string text, bool healthy)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(delegate { SetStatus(text, healthy); });
                return;
            }
            statusText.Text = text;
            statusPill.Background = new SolidColorBrush(healthy ? Mint : Pink);
        }

        private void StartMascotAnimation()
        {
            RotateTransform rotate = new RotateTransform(0, 41, 41);
            scanMascot.RenderTransform = rotate;
            DoubleAnimation animation = new DoubleAnimation(-3, 3, TimeSpan.FromMilliseconds(330));
            animation.AutoReverse = true;
            animation.RepeatBehavior = RepeatBehavior.Forever;
            rotate.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        private void StopMascotAnimation()
        {
            RotateTransform rotate = scanMascot.RenderTransform as RotateTransform;
            if (rotate != null) rotate.BeginAnimation(RotateTransform.AngleProperty, null);
            scanMascot.RenderTransform = Transform.Identity;
        }

        private static Border Badge(string text, Color background, Color foreground)
        {
            Border badge = new Border();
            badge.CornerRadius = new CornerRadius(8);
            badge.Background = new SolidColorBrush(background);
            badge.Padding = new Thickness(7, 2, 7, 2);
            badge.Margin = new Thickness(7, 0, 0, 0);
            TextBlock words = new TextBlock();
            words.Text = text;
            words.FontSize = 8.8;
            words.Foreground = new SolidColorBrush(foreground);
            badge.Child = words;
            return badge;
        }

        private static Button RoundedButton(string text, Color background,
            Color foreground, double radius)
        {
            Button button = new Button();
            button.Content = text;
            button.Background = new SolidColorBrush(background);
            button.Foreground = new SolidColorBrush(foreground);
            button.BorderThickness = new Thickness(0);
            button.Padding = new Thickness(13, 6, 13, 6);
            button.Cursor = Cursors.Hand;
            button.FontFamily = new FontFamily("Microsoft YaHei UI");
            button.FontSize = 11.5;

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetBinding(ContentPresenter.MarginProperty, new System.Windows.Data.Binding("Padding")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            border.AppendChild(presenter);
            template.VisualTree = border;
            button.Template = template;
            return button;
        }

        private static string KindName(EntryKind kind)
        {
            switch (kind)
            {
                case EntryKind.StaticVerb: return "普通命令";
                case EntryKind.ContextHandler: return "动态扩展";
                case EntryKind.ModernVerb: return "现代应用";
                case EntryKind.ShellNew: return "新建项目";
                case EntryKind.SendToFile: return "发送目标";
                case EntryKind.OpenWithApplication: return "打开应用";
                case EntryKind.WinXFile: return "系统快捷项";
                default: return kind.ToString();
            }
        }

        private static string FriendlyDisplayName(MenuEntry entry)
        {
            string value = string.IsNullOrWhiteSpace(entry.Name) ? "" : entry.Name.Trim();
            if (entry.Kind == EntryKind.ShellNew && value.StartsWith("."))
            {
                int separator = value.IndexOf(" ·", StringComparison.Ordinal);
                string extension = separator > 0 ? value.Substring(0, separator) : value;
                if (extension.Length <= 12)
                    return "新建 " + extension.TrimStart('.').ToUpperInvariant() + " 文件";
            }
            Guid ignored;
            if (Guid.TryParse(value.Trim('{', '}'), out ignored))
            {
                if (entry.Kind == EntryKind.ContextHandler) return "未命名动态菜单扩展";
                return "未命名右键菜单";
            }
            if (string.IsNullOrWhiteSpace(value)) return "未命名右键菜单";
            bool looksLikeCommand = value.IndexOf(@":\", StringComparison.Ordinal) >= 0 &&
                                    value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) >= 0;
            bool looksLikeHandlerKey = value.IndexOf("shell context menu",
                StringComparison.OrdinalIgnoreCase) >= 0;
            if (looksLikeCommand || looksLikeHandlerKey)
            {
                string fromSource = FriendlyTypeFromSource(entry.Source);
                if (!string.IsNullOrWhiteSpace(fromSource))
                    return fromSource + (entry.Kind == EntryKind.ContextHandler
                        ? " 菜单扩展" : " 菜单");
                return entry.Kind == EntryKind.ContextHandler
                    ? "未命名动态菜单扩展" : "未命名右键菜单";
            }
            int technicalCharacters = value.Count(character =>
                char.IsLetterOrDigit(character) || character == '-' ||
                character == '_' || character == '.' || character == '{' ||
                character == '}' || character == '@');
            if (value.Length > 32 && technicalCharacters >= value.Length * 9 / 10)
            {
                if (entry.Kind == EntryKind.ContextHandler)
                    return "未命名动态菜单扩展";
                if (entry.Kind == EntryKind.ModernVerb)
                    return "未命名应用菜单";
                return "未命名右键菜单";
            }
            return value;
        }

        private static string FriendlyTypeFromSource(string source)
        {
            source = source ?? "";
            string[] prefixes = new[] { "文件类型扩展 ", "文件类型 " };
            foreach (string prefix in prefixes)
            {
                if (!source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string value = source.Substring(prefix.Length);
                int separator = value.IndexOf(" ·", StringComparison.Ordinal);
                if (separator >= 0) value = value.Substring(0, separator);
                value = value.Trim();
                if (!string.IsNullOrWhiteSpace(value) && value.Length <= 42)
                    return value;
            }
            return "";
        }

        private static string CategoryIcon(string category)
        {
            if (category == CategoryNames.File) return "📄";
            if (category == CategoryNames.Folder) return "📁";
            if (category == CategoryNames.Directory) return "🗂";
            if (category == CategoryNames.DirectoryBackground) return "◫";
            if (category == CategoryNames.DesktopBackground) return "🖥";
            if (category == CategoryNames.Drive) return "💽";
            if (category == CategoryNames.ThisPc) return "💻";
            if (category == CategoryNames.RecycleBin) return "♻";
            if (category == CategoryNames.ImageMedia) return "🖼";
            if (category == CategoryNames.ModernApps) return "▦";
            if (category == CategoryNames.Software) return "▦";
            if (category == CategoryNames.NewMenu) return "＋";
            if (category == CategoryNames.SendTo) return "➤";
            if (category == CategoryNames.OpenWith) return "↗";
            if (category == CategoryNames.WinX) return "⊞";
            return "⚙";
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }
    }
}
