using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RandomMagicConversion;

internal sealed class MainForm : Form
{
    private readonly TextBox _inputPathBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _outputPathBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _gameExecutablePathBox = new() { Dock = DockStyle.Fill, PlaceholderText = @"...\steamapps\common\ELDEN RING\Game\eldenring.exe" };
    private readonly TextBox _randomizerExecutablePathBox = new() { Dock = DockStyle.Fill, PlaceholderText = @"...\EldenRingRandomizer.exe" };
    private readonly TextBox _seedBox = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _profileBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _poolModeBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _languageBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _shieldCheckBox = new() { Checked = true, AutoSize = true };
    private readonly CheckBox _shopCheckBox = new() { Checked = true, AutoSize = true };
    private readonly CheckBox _mapLootCheckBox = new() { Checked = true, AutoSize = true };
    private readonly CheckBox _bossCheckBox = new() { Checked = true, AutoSize = true };
    private readonly CheckBox _enemyLotCheckBox = new() { Checked = true, AutoSize = true };
    private readonly CheckBox _farmableEnemyLootCheckBox = new() { Checked = true, AutoSize = true };
    private readonly CheckBox _ashCheckBox = new() { Checked = true, AutoSize = true };
    private readonly CheckBox _startingClassCheckBox = new() { Checked = true, AutoSize = true };
    private readonly CheckBox _textCheckBox = new() { Checked = true, AutoSize = true, MaximumSize = new System.Drawing.Size(440, 0) };
    private readonly CheckBox _standaloneNoRequirementsCheckBox = new() { Checked = true, AutoSize = true, MaximumSize = new System.Drawing.Size(440, 0) };
    private readonly TextBox _optionHelpBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        WordWrap = true,
        BorderStyle = BorderStyle.FixedSingle,
        Height = 220,
        TabStop = false
    };
    private readonly TextBox _logBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        ReadOnly = true,
        WordWrap = false
    };
    private readonly Button _generateButton = new() { AutoSize = true };
    private readonly Button _launchButton = new() { AutoSize = true };
    private readonly Button _launchRandomizerButton = new() { AutoSize = true };
    private AppLanguage _language = AppLanguage.French;
    private string _modEngineLauncherOverridePath;

    public MainForm()
    {
        Text = VersionInfo.BuildWindowTitle(T("AppTitle"));
        ApplyWindowIcon();
        StartPosition = FormStartPosition.CenterScreen;
        Width = 980;
        Height = 900;

        _languageBox.Items.AddRange(new object[]
        {
            new ComboItem("Français", "fr"),
            new ComboItem("English", "en"),
            new ComboItem("中文", "zh")
        });
        _languageBox.SelectedIndex = 0;

        PopulateProfileBox();
        PopulatePoolModeBox();

        _generateButton.Click += GenerateButton_Click;
        _launchButton.Click += LaunchButton_Click;
        _launchRandomizerButton.Click += LaunchRandomizerButton_Click;
        _profileBox.SelectedIndexChanged += (_, _) => UpdateProfileUiState();
        _languageBox.SelectedIndexChanged += (_, _) => ChangeLanguageFromSelection();
        ApplyLocalizedTexts();

        BuildLayout();
        ApplyDefaults();
        UpdateProfileUiState();
    }

    private void ChangeLanguageFromSelection()
    {
        _language = GetSelectedValue(_languageBox) switch
        {
            "en" => AppLanguage.English,
            "zh" => AppLanguage.Chinese,
            _ => AppLanguage.French
        };

        ApplyLocalizedTexts();
        RebuildLayout();
        UpdateProfileUiState();
    }

    private void ApplyLocalizedTexts()
    {
        PopulateProfileBox();
        PopulatePoolModeBox();

        Text = VersionInfo.BuildWindowTitle(T("AppTitle"));
        _seedBox.PlaceholderText = T("Automatic");
        _shieldCheckBox.Text = T("ShieldOption");
        _shopCheckBox.Text = T("ShopOption");
        _mapLootCheckBox.Text = T("MapLootOption");
        _bossCheckBox.Text = T("BossOption");
        _enemyLotCheckBox.Text = T("EnemyLotOption");
        _farmableEnemyLootCheckBox.Text = T("FarmableEnemyLootOption");
        _ashCheckBox.Text = T("AshOption");
        _startingClassCheckBox.Text = T("StartingClassOption");
        _textCheckBox.Text = T("TextOption");
        _standaloneNoRequirementsCheckBox.Text = T("StandaloneNoRequirementsOption");
        _generateButton.Text = T("Generate");
        _launchButton.Text = T("LaunchGame");
        _launchRandomizerButton.Text = T("LaunchRandomizer");
        _optionHelpBox.Text = BuildOptionHelpText();
    }

    private void PopulateProfileBox()
    {
        string selectedValue = GetSelectedValue(_profileBox) ?? "Randomizer Friendly";
        _profileBox.Items.Clear();
        _profileBox.Items.AddRange(new object[]
        {
            new ComboItem(T("ProfileRandomizer"), "Randomizer Friendly"),
            new ComboItem(T("ProfileStandalone"), "Standalone")
        });
        SelectComboValue(_profileBox, selectedValue);
    }

    private void PopulatePoolModeBox()
    {
        string selectedValue = GetSelectedValue(_poolModeBox) ?? ShopMagicPoolMode.Both.ToString();
        _poolModeBox.Items.Clear();
        _poolModeBox.Items.AddRange(new object[]
        {
            new ComboItem(T("PoolBoth"), ShopMagicPoolMode.Both.ToString()),
            new ComboItem(T("PoolSorcery"), ShopMagicPoolMode.SorceryOnly.ToString()),
            new ComboItem(T("PoolIncantation"), ShopMagicPoolMode.IncantationOnly.ToString())
        });
        SelectComboValue(_poolModeBox, selectedValue);
    }

    private static void SelectComboValue(ComboBox comboBox, string value)
    {
        for (int index = 0; index < comboBox.Items.Count; index++)
        {
            if (comboBox.Items[index] is ComboItem item &&
                string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = index;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
            comboBox.SelectedIndex = 0;
    }

    private void RebuildLayout()
    {
        SuspendLayout();
        try
        {
            Controls.Clear();
            BuildLayout();
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    private string T(string key)
    {
        return UiText.Get(_language, key);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        root.Controls.Add(BuildPathsGroup(), 0, 0);
        root.Controls.Add(BuildOptionsGroup(), 0, 1);
        root.Controls.Add(BuildActionsGroup(), 0, 2);
        root.Controls.Add(BuildLogsGroup(), 0, 3);

        Controls.Add(root);
    }

    private Control BuildPathsGroup()
    {
        var group = new GroupBox
        {
            Text = T("Paths"),
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            AutoSize = true
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AddPathRow(table, 0, T("InputRegulation"), _inputPathBox, () => BrowseInputPath());
        AddPathRow(table, 1, T("OutputRegulation"), _outputPathBox, () => BrowseOutputPath());
        AddPathRow(table, 2, T("GameExecutable"), _gameExecutablePathBox, () => BrowseGameExecutablePath());
        AddPathRow(table, 3, T("RandomizerExecutable"), _randomizerExecutablePathBox, () => BrowseRandomizerExecutablePath());

        group.Controls.Add(table);
        return group;
    }

    private Control BuildOptionsGroup()
    {
        var group = new GroupBox
        {
            Text = T("Options"),
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            AutoSize = true
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        table.Controls.Add(BuildLabeledField(
            T("Profile"),
            _profileBox,
            T("ProfileHelp"),
            58), 0, 0);
        table.Controls.Add(BuildLabeledField(
            T("MagicType"),
            _poolModeBox,
            T("MagicTypeHelp"),
            48), 1, 0);
        table.Controls.Add(BuildLabeledField(
            T("Seed"),
            _seedBox,
            T("SeedHelp"),
            58), 0, 1);
        table.Controls.Add(BuildLabeledField(
            T("Language"),
            _languageBox,
            T("LanguageHelp"),
            42), 0, 2);

        var pipelinePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false
        };
        pipelinePanel.Controls.Add(_shieldCheckBox);
        pipelinePanel.Controls.Add(_shopCheckBox);
        pipelinePanel.Controls.Add(_mapLootCheckBox);
        pipelinePanel.Controls.Add(_bossCheckBox);
        pipelinePanel.Controls.Add(_enemyLotCheckBox);
        pipelinePanel.Controls.Add(_farmableEnemyLootCheckBox);
        pipelinePanel.Controls.Add(_ashCheckBox);
        pipelinePanel.Controls.Add(_startingClassCheckBox);
        pipelinePanel.Controls.Add(_textCheckBox);
        pipelinePanel.Controls.Add(_standaloneNoRequirementsCheckBox);

        var optionField = BuildLabeledField(T("Option"), pipelinePanel);
        table.Controls.Add(optionField, 1, 1);
        table.SetRowSpan(optionField, 3);
        table.Controls.Add(BuildLabeledField(T("OptionDescription"), _optionHelpBox), 0, 3);

        group.Controls.Add(table);
        return group;
    }

    private string BuildOptionHelpText()
    {
        var helpLines = new[]
        {
            T("HelpShield"),
            T("HelpShop"),
            T("HelpMapLoot"),
            T("HelpBoss"),
            T("HelpEnemyLot"),
            T("HelpFarmableEnemyLoot"),
            T("HelpAsh"),
            T("HelpStartingClass"),
            T("HelpText")
        }.ToList();

        if (IsStandaloneProfile())
            helpLines.Add(T("HelpStandaloneNoRequirements"));

        return string.Join(Environment.NewLine + Environment.NewLine, helpLines);
    }

    private Control BuildActionsGroup()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };
        panel.Controls.Add(_generateButton);
        panel.Controls.Add(_launchButton);
        panel.Controls.Add(_launchRandomizerButton);
        return panel;
    }

    private Control BuildLogsGroup()
    {
        var group = new GroupBox
        {
            Text = T("Log"),
            Dock = DockStyle.Fill
        };
        group.Controls.Add(_logBox);
        return group;
    }

    private static Control BuildLabeledField(string label, Control field, string helpText = null, int helpHeight = 56)
    {
        bool hasHelp = !string.IsNullOrWhiteSpace(helpText);
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = hasHelp ? 3 : 2,
            AutoSize = true
        };
        panel.Controls.Add(new Label { Text = label, AutoSize = true }, 0, 0);
        panel.Controls.Add(field, 0, 1);
        if (hasHelp)
            panel.Controls.Add(BuildHelpBox(helpText, helpHeight), 0, 2);

        return panel;
    }

    private static Control BuildHelpBox(string text, int height)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = height,
            Margin = new Padding(0, 4, 0, 0)
        };
    }

    private void AddPathRow(TableLayoutPanel table, int rowIndex, string label, TextBox textBox, Action onBrowse)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, rowIndex);
        table.Controls.Add(textBox, 1, rowIndex);

        var button = new Button { Text = T("Browse"), AutoSize = true };
        button.Click += (_, _) =>
        {
            try
            {
                onBrowse();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(T("BrowseError"), ex.Message),
                    "SIDERAL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };
        table.Controls.Add(button, 2, rowIndex);
    }

    private void ApplyDefaults()
    {
        string projectDir = ProjectLayout.ResolveProjectDir();
        _inputPathBox.Text = Path.Combine(projectDir, "Base", "regulation_base.bin");
        _outputPathBox.Text = Path.Combine(projectDir, "Output", "regulation.bin");

        string steamBasePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (string.IsNullOrWhiteSpace(steamBasePath))
            steamBasePath = @"C:\Program Files (x86)";

        _gameExecutablePathBox.Text = Path.Combine(steamBasePath, "Steam", "steamapps", "common", "ELDEN RING", "Game", "eldenring.exe");
        _randomizerExecutablePathBox.Text = FindDefaultRandomizerExecutablePath(projectDir) ?? string.Empty;
    }

    private void BrowseInputPath()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = $"{T("RegulationFilter")}|*.bin|{T("AllFilesFilter")}|*.*",
            FileName = _inputPathBox.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _inputPathBox.Text = dialog.FileName;
    }

    private void BrowseOutputPath()
    {
        string initialDirectory = Path.GetDirectoryName(_outputPathBox.Text);
        using var dialog = new SaveFileDialog
        {
            Filter = $"{T("RegulationFilter")}|*.bin|{T("AllFilesFilter")}|*.*",
            FileName = Path.GetFileName(_outputPathBox.Text),
            InitialDirectory = !string.IsNullOrWhiteSpace(initialDirectory) ? initialDirectory : ProjectLayout.ResolveProjectDir()
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _outputPathBox.Text = dialog.FileName;
    }

    private void BrowseGameExecutablePath()
    {
        string currentPath = _gameExecutablePathBox.Text.Trim();
        string currentDirectory = Path.GetDirectoryName(currentPath);
        string defaultDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "steamapps",
            "common",
            "ELDEN RING",
            "Game");

        using var dialog = new OpenFileDialog
        {
            Filter = $"eldenring.exe|eldenring.exe|{T("ExecutableFilter")}|*.exe|{T("AllFilesFilter")}|*.*",
            FileName = string.IsNullOrWhiteSpace(currentPath) ? "eldenring.exe" : Path.GetFileName(currentPath),
            InitialDirectory = Directory.Exists(currentDirectory)
                ? currentDirectory
                : Directory.Exists(defaultDirectory)
                    ? defaultDirectory
                    : ProjectLayout.ResolveProjectDir()
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _gameExecutablePathBox.Text = dialog.FileName;
    }

    private void BrowseRandomizerExecutablePath()
    {
        string currentPath = _randomizerExecutablePathBox.Text.Trim();
        string currentDirectory = Path.GetDirectoryName(currentPath);
        string defaultDirectory = Path.GetDirectoryName(FindDefaultRandomizerExecutablePath(ProjectLayout.ResolveProjectDir()) ?? string.Empty);

        using var dialog = new OpenFileDialog
        {
            Filter = $"EldenRingRandomizer.exe|EldenRingRandomizer.exe|{T("ExecutableFilter")}|*.exe|{T("AllFilesFilter")}|*.*",
            FileName = string.IsNullOrWhiteSpace(currentPath) ? "EldenRingRandomizer.exe" : Path.GetFileName(currentPath),
            InitialDirectory = Directory.Exists(currentDirectory)
                ? currentDirectory
                : Directory.Exists(defaultDirectory)
                    ? defaultDirectory
                    : ProjectLayout.ResolveProjectDir()
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _randomizerExecutablePathBox.Text = dialog.FileName;
    }

    private async void GenerateButton_Click(object sender, EventArgs e)
    {
        try
        {
            GenerationRequest request = BuildRequest();

            ToggleControls(false);
            _logBox.Clear();
            AppendLog(T("GenerationStarted"));
            AppendLog(string.Empty);

            GenerationResult result;
            using (var redirect = new ConsoleRedirectScope(new CallbackTextWriter(AppendLog)))
            {
                result = await Task.Run(() => new GenerationRunner().Run(request));
            }

            AppendLog(string.Empty);
            AppendLog(string.Format(T("GenerationFinishedLog"), result.Seed));
            AppendLog(string.Format(T("GeneratedFileLog"), result.OutputRegulationPath));

            MessageBox.Show(
                this,
                string.Format(T("GenerationDoneMessage"), result.Seed, result.OutputRegulationPath),
                "SIDERAL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppendLog(string.Empty);
            AppendLog(string.Format(T("GenerationErrorLog"), ex.Message));

            MessageBox.Show(
                this,
                ex.ToString(),
                T("GenerationErrorTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            ToggleControls(true);
        }
    }

    private void LaunchButton_Click(object sender, EventArgs e)
    {
        try
        {
            if (!IsStandaloneProfile())
            {
                MessageBox.Show(
                    this,
                    T("StandaloneOnlyLaunch"),
                    "SIDERAL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string gameExecutablePath = _gameExecutablePathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(gameExecutablePath))
                throw new InvalidOperationException(T("GamePathRequired"));

            string gameFolder = Path.GetDirectoryName(gameExecutablePath);
            if (string.IsNullOrWhiteSpace(gameFolder))
                throw new InvalidOperationException(T("GameFolderInvalid"));

            string generatedRegulationPath = ResolveGeneratedRegulationPath();
            if (!File.Exists(generatedRegulationPath))
                throw new FileNotFoundException(T("GeneratedRegulationMissing"), generatedRegulationPath);

            string modFolder = Path.GetDirectoryName(generatedRegulationPath);
            if (string.IsNullOrWhiteSpace(modFolder))
                throw new InvalidOperationException(T("OutputFolderMissing"));

            string configPath = Path.Combine(modFolder, "config_sideral.toml");
            WriteModEngineConfig(configPath, modFolder);

            string modEngineLauncherPath = ResolveModEngineLauncherPath(gameExecutablePath);
            string modEngineFolder = Path.GetDirectoryName(modEngineLauncherPath);
            if (string.IsNullOrWhiteSpace(modEngineFolder))
                throw new InvalidOperationException(T("ModEngineFolderMissing"));

            var startInfo = new ProcessStartInfo
            {
                FileName = modEngineLauncherPath,
                Arguments = $"-t er -c \"{configPath}\"",
                WorkingDirectory = modEngineFolder,
                UseShellExecute = false
            };

            Process.Start(startInfo);

            AppendLog(string.Empty);
            AppendLog(string.Format(T("ModEngineConfigLog"), configPath));
            AppendLog(string.Format(T("InjectedModFolderLog"), modFolder));
            AppendLog(string.Format(T("GameExecutableLog"), gameExecutablePath));
            AppendLog(T("ModEngineLaunchRequested"));
        }
        catch (Exception ex)
        {
            AppendLog(string.Empty);
            AppendLog(string.Format(T("LaunchErrorLog"), ex.Message));

            MessageBox.Show(
                this,
                ex.ToString(),
                T("LaunchErrorTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void LaunchRandomizerButton_Click(object sender, EventArgs e)
    {
        try
        {
            if (IsStandaloneProfile())
            {
                MessageBox.Show(
                    this,
                    T("RandomizerOnlyLaunch"),
                    "SIDERAL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string randomizerExecutablePath = _randomizerExecutablePathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(randomizerExecutablePath))
                throw new InvalidOperationException(T("RandomizerPathRequired"));

            if (!File.Exists(randomizerExecutablePath))
                throw new FileNotFoundException(T("RandomizerMissing"), randomizerExecutablePath);

            string randomizerFolder = Path.GetDirectoryName(randomizerExecutablePath);
            if (string.IsNullOrWhiteSpace(randomizerFolder))
                throw new InvalidOperationException(T("RandomizerFolderInvalid"));

            var startInfo = new ProcessStartInfo
            {
                FileName = randomizerExecutablePath,
                WorkingDirectory = randomizerFolder,
                UseShellExecute = true
            };

            Process.Start(startInfo);

            AppendLog(string.Empty);
            AppendLog(string.Format(T("RandomizerLaunchLog"), randomizerExecutablePath));
        }
        catch (Exception ex)
        {
            AppendLog(string.Empty);
            AppendLog(string.Format(T("RandomizerLaunchErrorLog"), ex.Message));

            MessageBox.Show(
                this,
                ex.ToString(),
                T("RandomizerLaunchErrorTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private GenerationRequest BuildRequest()
    {
        string projectDir = ProjectLayout.ResolveProjectDir();
        (string shop, string mapLoot, string boss, string enemyLot, string farmableEnemyLoot, string ash, string startingClass) = ResolveProfileConfigPaths(projectDir);
        bool standalone = IsStandaloneProfile();

        int? seedOverride = null;
        if (!string.IsNullOrWhiteSpace(_seedBox.Text))
        {
            if (!int.TryParse(_seedBox.Text.Trim(), out int parsedSeed))
                throw new InvalidOperationException(T("SeedInvalid"));

            seedOverride = parsedSeed;
        }

        return new GenerationRequest
        {
            ProjectDir = projectDir,
            InputRegulationPath = _inputPathBox.Text.Trim(),
            OutputRegulationPath = _outputPathBox.Text.Trim(),
            ShopConfigPath = shop,
            MapLootConfigPath = mapLoot,
            BossRewardConfigPath = boss,
            EnemyLotConfigPath = enemyLot,
            FarmableEnemyLootSuppressionConfigPath = farmableEnemyLoot,
            AshOfWarConfigPath = ash,
            StartingClassConfigPath = startingClass,
            SeedOverride = seedOverride,
            EnableShieldConversion = _shieldCheckBox.Checked,
            EnableShopConversion = _shopCheckBox.Checked,
            EnableMapLootConversion = _mapLootCheckBox.Checked,
            EnableBossRewardConversion = _bossCheckBox.Checked,
            EnableEnemyLotConversion = _enemyLotCheckBox.Checked,
            EnableFarmableEnemyLootSuppression = _farmableEnemyLootCheckBox.Checked,
            EnableAshOfWarConversion = _ashCheckBox.Checked,
            EnableStartingClassConversion = _startingClassCheckBox.Checked,
            GenerateTextOutputs = _textCheckBox.Checked,
            UseRandomizerFriendlyShieldUpgradePath = !standalone,
            RemoveStandaloneStatRequirements = standalone && _standaloneNoRequirementsCheckBox.Checked,
            SpellPoolModeOverride = GetSelectedValue(_poolModeBox)
        };
    }

    private (string Shop, string MapLoot, string Boss, string EnemyLot, string FarmableEnemyLoot, string Ash, string StartingClass) ResolveProfileConfigPaths(string projectDir)
    {
        string dataDir = Path.Combine(projectDir, "Data");
        bool standalone = IsStandaloneProfile();

        if (standalone)
        {
            return
            (
                Path.Combine(dataDir, "shop_weapon_to_magic.standalone.json"),
                Path.Combine(dataDir, "map_loot_weapon_to_magic.standalone.json"),
                Path.Combine(dataDir, "boss_reward_weapon_to_magic.standalone.json"),
                Path.Combine(dataDir, "enemy_lot_weapon_to_magic.standalone.json"),
                Path.Combine(dataDir, "farmable_enemy_loot_suppression.standalone.json"),
                Path.Combine(dataDir, "ash_of_war_to_magic.standalone.json"),
                Path.Combine(dataDir, "starting_class_weapon_to_magic.standalone.json")
            );
        }

        return
        (
            Path.Combine(dataDir, "shop_weapon_to_magic.json"),
            Path.Combine(dataDir, "map_loot_weapon_to_magic.json"),
            Path.Combine(dataDir, "boss_reward_weapon_to_magic.json"),
            Path.Combine(dataDir, "enemy_lot_weapon_to_magic.json"),
            Path.Combine(dataDir, "farmable_enemy_loot_suppression.json"),
            Path.Combine(dataDir, "ash_of_war_to_magic.json"),
            Path.Combine(dataDir, "starting_class_weapon_to_magic.json")
        );
    }

    private void ToggleControls(bool enabled)
    {
        bool standalone = IsStandaloneProfile();
        _generateButton.Enabled = enabled;
        _launchButton.Visible = standalone;
        _launchButton.Enabled = enabled && standalone;
        _launchRandomizerButton.Visible = !standalone;
        _launchRandomizerButton.Enabled = enabled && !standalone;
        _inputPathBox.Enabled = enabled;
        _outputPathBox.Enabled = enabled;
        _gameExecutablePathBox.Enabled = enabled;
        _randomizerExecutablePathBox.Enabled = enabled;
        _seedBox.Enabled = enabled;
        _profileBox.Enabled = enabled;
        _poolModeBox.Enabled = enabled;
        _languageBox.Enabled = enabled;
        _shieldCheckBox.Enabled = enabled;
        _shopCheckBox.Enabled = enabled;
        _mapLootCheckBox.Enabled = enabled;
        _bossCheckBox.Enabled = enabled;
        _enemyLotCheckBox.Enabled = enabled;
        _farmableEnemyLootCheckBox.Enabled = enabled;
        _ashCheckBox.Enabled = enabled;
        _startingClassCheckBox.Enabled = enabled;
        _textCheckBox.Enabled = enabled;
        _standaloneNoRequirementsCheckBox.Visible = standalone;
        _standaloneNoRequirementsCheckBox.Enabled = enabled && standalone;
    }

    private void UpdateProfileUiState()
    {
        bool standalone = IsStandaloneProfile();
        _launchButton.Visible = standalone;
        _launchButton.Enabled = standalone;
        _launchRandomizerButton.Visible = !standalone;
        _launchRandomizerButton.Enabled = !standalone;
        _standaloneNoRequirementsCheckBox.Visible = standalone;
        _standaloneNoRequirementsCheckBox.Enabled = standalone;
        _optionHelpBox.Text = BuildOptionHelpText();
    }

    private void ApplyWindowIcon()
    {
        try
        {
            using Icon associatedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (associatedIcon != null)
                Icon = (Icon)associatedIcon.Clone();
        }
        catch
        {
        }
    }

    private void AppendLog(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), line);
            return;
        }

        _logBox.AppendText(line + Environment.NewLine);
    }

    private static string GetSelectedValue(ComboBox comboBox)
    {
        return comboBox.SelectedItem is ComboItem item
            ? item.Value
            : comboBox.SelectedItem?.ToString();
    }

    private bool IsStandaloneProfile()
    {
        return string.Equals(GetSelectedValue(_profileBox), "Standalone", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindDefaultRandomizerExecutablePath(string projectDir)
    {
        string parentDir = Directory.GetParent(projectDir)?.FullName;
        string[] candidates =
        {
            string.IsNullOrWhiteSpace(parentDir) ? null : Path.Combine(parentDir, "EldenRingRandomizer.exe"),
            string.IsNullOrWhiteSpace(parentDir) ? null : Path.Combine(parentDir, "Randomizer", "EldenRingRandomizer.exe"),
            string.IsNullOrWhiteSpace(parentDir) ? null : Path.Combine(parentDir, "Elden Ring Item and Enemy Randomizer", "EldenRingRandomizer.exe"),
            string.IsNullOrWhiteSpace(parentDir) ? null : Path.Combine(parentDir, "EldenRingRandomizer", "EldenRingRandomizer.exe"),
            Path.Combine(projectDir, "EldenRingRandomizer.exe")
        };

        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private string ResolveGeneratedRegulationPath()
    {
        string outputPath = _outputPathBox.Text.Trim();
        string resolvedPath = !string.IsNullOrWhiteSpace(outputPath)
            ? outputPath
            : Path.Combine(ProjectLayout.ResolveProjectDir(), "Output", "regulation.bin");

        return Path.GetFullPath(resolvedPath);
    }

    private static void WriteModEngineConfig(string configPath, string modFolder)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath));

        string normalizedModFolder = modFolder.Replace('\\', '/');
        string configText = string.Join(Environment.NewLine, new[]
        {
            "[modengine]",
            "debug = false",
            "external_dlls = []",
            "",
            "[extension.mod_loader]",
            "enabled = true",
            "loose_params = false",
            "mods = [",
            $"    {{ enabled = true, name = \"SIDERAL\", path = \"{normalizedModFolder}\" }}",
            "]",
            "",
            "[extension.scylla_hide]",
            "enabled = false",
            ""
        });

        File.WriteAllText(configPath, configText);
    }

    private string ResolveModEngineLauncherPath(string gameExecutablePath)
    {
        string appDir = AppContext.BaseDirectory;
        string gameFolder = Path.GetDirectoryName(gameExecutablePath);

        string[] candidates =
        {
            _modEngineLauncherOverridePath,
            Path.Combine(appDir, "ModEngine", "modengine2_launcher.exe"),
            Path.Combine(appDir, "modengine2_launcher.exe"),
            Path.Combine(appDir, "modengine2", "modengine2_launcher.exe"),
            Path.Combine(Path.GetFullPath(Path.Combine(appDir, "..")), "ModEngine", "modengine2_launcher.exe"),
            string.IsNullOrWhiteSpace(gameFolder) ? null : Path.Combine(gameFolder, "modengine2_launcher.exe"),
            string.IsNullOrWhiteSpace(gameFolder) ? null : Path.Combine(gameFolder, "ModEngine", "modengine2_launcher.exe")
        };

        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                return candidate;
        }

        string selectedPath = BrowseForModEngineLauncherPath(gameFolder);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            _modEngineLauncherOverridePath = selectedPath;
            return selectedPath;
        }

        throw new FileNotFoundException(
            T("ModEngineNotFound"));
    }

    private string BrowseForModEngineLauncherPath(string gameFolder)
    {
        string appModEngineFolder = Path.Combine(AppContext.BaseDirectory, "ModEngine");
        using var dialog = new OpenFileDialog
        {
            Title = T("ModEngineDialogTitle"),
            Filter = $"modengine2_launcher.exe|modengine2_launcher.exe|{T("ExecutableFilter")}|*.exe|{T("AllFilesFilter")}|*.*",
            FileName = "modengine2_launcher.exe",
            InitialDirectory = Directory.Exists(appModEngineFolder)
                ? appModEngineFolder
                : Directory.Exists(gameFolder)
                    ? gameFolder
                    : AppContext.BaseDirectory
        };

        return dialog.ShowDialog(this) == DialogResult.OK
            ? dialog.FileName
            : null;
    }

    private sealed class ComboItem
    {
        public ComboItem(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }

        public string Value { get; }

        public override string ToString()
        {
            return Label;
        }
    }
}
