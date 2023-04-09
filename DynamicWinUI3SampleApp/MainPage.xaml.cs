using Bogus;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using OneOf;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DynamicWinUI3SampleApp;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    private static OneOf<FrameworkElement, string> CreateUIContent(string xamlText)
    {
        try
        {
            return XamlReader.Load(xamlText) is FrameworkElement frameworkElement
                ? frameworkElement
                : "The provided XAML does not create a FrameworkElement.";
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }

    private static OneOf<Assembly, ImmutableArray<Diagnostic>, string> CreateAssembly(string csharpText)
    {
        try
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName: "SomeAssembly",
                options: new CSharpCompilationOptions(
                    outputKind: OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable),
                references: CreateReferences());

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(csharpText);
            compilation = compilation.AddSyntaxTrees(syntaxTree);

            using MemoryStream stream = new();
            EmitResult emitResult = compilation.Emit(stream);

            if (emitResult.Success is false)
            {
                return emitResult.Diagnostics;
            }

            _ = stream.Seek(0, SeekOrigin.Begin);

            return Assembly.Load(stream.ToArray());
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }

    private static List<PortableExecutableReference> CreateReferences()
    {
        string runtimePath = RuntimeEnvironment.GetRuntimeDirectory();
        var referenceFileLocations = new[]
        {
            typeof(object).GetTypeInfo().Assembly.Location,
            Path.Combine(runtimePath, "System.Runtime.dll"),
            typeof(PropertyChangedEventHandler).GetTypeInfo().Assembly.Location,

            typeof(Faker).GetTypeInfo().Assembly.Location,
            Path.Combine(runtimePath, "System.Linq.Expressions.dll"),
            Path.Combine(runtimePath, "System.Collections.dll"),
        };

        List<PortableExecutableReference> references = new();

        foreach (string path in referenceFileLocations)
        {
            references.Add(MetadataReference.CreateFromFile(path));
        }

        return references;
    }

    private void Sample1Button_Click(object sender, RoutedEventArgs e)
    {
        this.DynamicContentFrame.Content = null;

        this.XamlCodeTextBox.Text =
            """"
            <Grid
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                RowDefinitions="Auto,Auto">
                <TextBox
                    Grid.Row="0"
                    Text="{Binding SomeText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                    PlaceholderText="Enter some text here..."/>
                <TextBlock
                    Grid.Row="1"
                    Text="{Binding SomeText, Mode=OneWay}" />
            </Grid>
            """";

        this.CSharpCodeTextBox.Text =
            """
            using System.ComponentModel;
            using System.Runtime.CompilerServices;

            public partial class ViewModelBase : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;

                protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                }
            }

            public partial class ViewModel : ViewModelBase
            {
                private string someText = string.Empty;

                public string SomeText
                {
                    get => this.someText;
                    set
                    {
                        if (this.someText != value)
                        {
                            this.someText = value;
                            OnPropertyChanged();
                        }
                    }
                }
            }
            """;
    }

    private void Sample2Button_Click(object sender, RoutedEventArgs e)
    {
        this.DynamicContentFrame.Content = null;

        this.XamlCodeTextBox.Text =
            """"
            <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBlock Text="{Binding Number, Mode=OneWay}" />
                <Button
                    Content="Increment"
                    Command="{Binding IncrementCommand}" />
            </StackPanel>
            """";

        this.CSharpCodeTextBox.Text =
            """
            using System;
            using System.ComponentModel;
            using System.Runtime.CompilerServices;
            using System.Windows.Input;

            public interface IRelayCommand : ICommand
            {
                void NotifyCanExecuteChanged();
            }

            public class RelayCommand : IRelayCommand
            {
                private readonly Action execute;

                private readonly Func<bool>? canExecute;

                public RelayCommand(Action execute)
                {
                    this.execute = execute;
                }

                public RelayCommand(Action execute, Func<bool> canExecute)
                {
                    this.execute = execute;
                    this.canExecute = canExecute;
                }

                public event EventHandler? CanExecuteChanged;

                public void NotifyCanExecuteChanged()
                {
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                }

                public bool CanExecute(object? parameter)
                {
                    return this.canExecute?.Invoke() is not false;
                }

                public void Execute(object? parameter)
                {
                    this.execute();
                }
            }

            public partial class ViewModelBase : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;

                protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                }
            }

            public partial class ViewModel : ViewModelBase
            {
                private int number;

                public int Number
                {
                    get => this.number;
                    set
                    {
                        if (this.number != value)
                        {
                            this.number = value;
                            OnPropertyChanged();
                        }
                    }
                }

                private RelayCommand? incrementCommand;

                public IRelayCommand IncrementCommand => this.incrementCommand ??= new RelayCommand(new Action(Increment));

                private void Increment()
                {
                    Number++;
                }
            }
            """;
    }

    private void Sample3Button_Click(object sender, RoutedEventArgs e)
    {
        this.DynamicContentFrame.Content = null;

        this.XamlCodeTextBox.Text =
            """"
            <Grid
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:controls="using:CommunityToolkit.WinUI.UI.Controls"
                RowDefinitions="Auto,*">
                <Button
                    Grid.Row="0"
                    Content="Load items"
                    Command="{Binding LoadCommand}" />
                <controls:DataGrid
                    Grid.Row="1"
                    ItemsSource="{Binding Users, Mode=OneWay}" />
            </Grid>
            """";

        this.CSharpCodeTextBox.Text =
            """
            using Bogus;
            using System;
            using System.Collections.Generic;
            using System.Collections.ObjectModel;
            using System.ComponentModel;
            using System.Runtime.CompilerServices;
            using System.Windows.Input;

            public interface IRelayCommand : ICommand
            {
                void NotifyCanExecuteChanged();
            }

            public class RelayCommand : IRelayCommand
            {
                private readonly Action execute;

                private readonly Func<bool>? canExecute;

                public RelayCommand(Action execute)
                {
                    this.execute = execute;
                }

                public RelayCommand(Action execute, Func<bool> canExecute)
                {
                    this.execute = execute;
                    this.canExecute = canExecute;
                }

                public event EventHandler? CanExecuteChanged;

                public void NotifyCanExecuteChanged()
                {
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                }

                public bool CanExecute(object? parameter)
                {
                    return this.canExecute?.Invoke() is not false;
                }

                public void Execute(object? parameter)
                {
                    this.execute();
                }
            }

            public partial class ViewModelBase : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;

                protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                }
            }

            public record User
            {
                public int Id { get; set; }

                public string FirstName { get; set; } = string.Empty;

                public string LastName { get; set; } = string.Empty;

                public string Address { get; set; } = string.Empty;
            }

            public partial class ViewModel : ViewModelBase
            {
                private ObservableCollection<User> users = new ObservableCollection<User>();

                public ObservableCollection<User> Users
                {
                    get => this.users;
                    set
                    {
                        if (this.users != value)
                        {
                            this.users = value;
                            OnPropertyChanged();
                        }
                    }
                }

                private RelayCommand? loadCommand;

                public IRelayCommand LoadCommand => this.loadCommand ??= new RelayCommand(new Action(Load));

                private void Load()
                {
                    Users = new ObservableCollection<User>(
                        new Faker<User>()
                            .UseSeed(0)
                            .RuleFor(u => u.Id, f => f.IndexFaker + 1)
                            .RuleFor(user => user.FirstName, faker => faker.Name.FirstName())
                            .RuleFor(user => user.LastName, faker => faker.Name.LastName())
                            .RuleFor(user => user.Address, faker => faker.Address.State())
                            .Generate(100));
                }
            }
            """;
    }

    private void CreateDynamicContentsButton_Click(object sender, RoutedEventArgs e)
    {
        this.ErrorsInfoBar.IsOpen = false;

        CreateUIContent(this.XamlCodeTextBox.Text).Switch(
            frameworkElement =>
            {
                CreateAssembly(this.CSharpCodeTextBox.Text).Switch(
                    assembly =>
                    {
                        object? viewModelInstance = assembly.CreateInstance("ViewModel");
                        frameworkElement.DataContext = viewModelInstance;
                        this.DynamicContentFrame.Content = frameworkElement;
                    },
                    diagnostics =>
                    {
                        this.ErrorsInfoBarItemsRepeater.ItemsSource = diagnostics;
                        this.ErrorsInfoBar.IsOpen = true;
                    },
                    errorMessage =>
                    {
                        this.ErrorsInfoBarItemsRepeater.ItemsSource = new[] { errorMessage };
                        this.ErrorsInfoBar.IsOpen = true;
                    });
            },
            errorMessage =>
            {
                this.ErrorsInfoBarItemsRepeater.ItemsSource = new[] { errorMessage };
                this.ErrorsInfoBar.IsOpen = true;
            });
    }
}