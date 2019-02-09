using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfApp1.PresageSvc;

namespace WpfApp1
{
    public enum AngleType
    {
        Flat,
        MidHeight,
        High
    }

    

    public class WristParser : INotifyPropertyChanged
    {
        private SerialPort port;
        public AngleType Angle
        {
            get => this.angle;
            set
            {
                if(value == this.angle)
                {
                    return;
                }

                this.OldAngle = this.angle;
                this.angle = value;
                this.AngleChanged?.Invoke(value, this.OldAngle);
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AngleFill"));
            }
        }

        public AngleType OldAngle { get; private set; }
            = AngleType.Flat;

        public event Action<string> GestureReceived;
        public event Action<int> FocusContainer;
        public event Action<string> GeneralMessageReceived;
        public event Action<AngleType, AngleType> AngleChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public int NumContainers { get; set; }

        public bool Run { get; set; } = false;

        private Task bgTask = null;
        private AngleType angle = AngleType.Flat;

        private int currentAngle = 0;
        public int CurrentAngle
        {
            get => this.currentAngle;
            set
            {
                if(value == this.currentAngle)
                {
                    return;
                }
                this.currentAngle = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentAngle"));
            }
        }

        private int currentRoll = 0;
        public int CurrentRoll
        {
            get => this.currentRoll;
            set
            {
                if (value == this.currentRoll)
                {
                    return;
                }
                this.currentRoll = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentRoll"));
            }
        }

        private SolidColorBrush lowAngle = new SolidColorBrush(Colors.Red);
        private SolidColorBrush medAngle = new SolidColorBrush(Colors.Yellow);
        private SolidColorBrush hiAngle = new SolidColorBrush(Colors.Green);

        public SolidColorBrush AngleFill
        {
            get
            {
                switch (this.Angle) { 
                    case AngleType.Flat:
                        return this.lowAngle;
                    case AngleType.MidHeight:
                        return this.medAngle;
                    case AngleType.High:
                        return this.hiAngle;
                    default:
                        return this.hiAngle;
                }
            }
        }

        public WristParser()
        {

        }

        private void Log(string toLog)
        {
            this.GeneralMessageReceived?.Invoke(toLog);
        }

        public bool StartListen(int comPort)
        {
            try
            {
                this.Run = false;

                if (bgTask != null)
                {
                    bgTask.Wait();
                }

                if (port != null && port.IsOpen)
                {
                    this.Log($"Close existing connection.");
                    port.Close();
                }

                this.Log($"Attempting connect on COM{comPort}...");
                port = new SerialPort($"COM{comPort}", 115200);
                port.Open();
                this.Log($"Connected COM{comPort}.");

                this.Run = true;

                this.bgTask = new TaskFactory().StartNew(() =>
                {
                    while (this.Run)
                    {
                        var line = port.ReadLine();
                        var items = line.Split(':');

                        if (items.Length < 2)
                        {
                            continue;
                        }

                        var opCode = items[0];

                        if (opCode == "0")
                        {
                            this.GeneralMessageReceived?.Invoke(line);
                            // Skip: diagnostic message.

                        }
                        else if (opCode == "1" && items.Length == 2)
                        {
                            // Gesture.
                            if (items[1] == "SHAKE")
                            {
                                this.Run = false;
                            }
                            this.GestureReceived?.Invoke(items[1]);

                            if (!this.Run)
                            {
                                return;
                            }
                        }
                        else if (opCode == "2" && items.Length == 3)
                        {
                            try
                            {
                                var roll = int.Parse(items[1]);
                                var pitch = int.Parse(items[2]);

                                this.CurrentAngle = pitch;
                                this.CurrentRoll = roll;

                                if (this.NumContainers > 0)
                                {
                                    if (pitch < 80)
                                    {
                                        this.Angle = AngleType.Flat;
                                        var maxValue = 360;
                                        var divisions = maxValue / this.NumContainers;

                                        var containerIndex = roll / divisions;

                                        if (containerIndex >= this.NumContainers)
                                            containerIndex = this.NumContainers - 1;
                                        else if (containerIndex < 0)
                                            containerIndex = 0;

                                        this.FocusContainer?.Invoke(containerIndex);

                                    }
                                    else if (pitch >= 100 && pitch < 300)
                                    {
                                        this.Angle = AngleType.MidHeight;
                                    }
                                    else if (pitch >= 320)
                                    {
                                        this.Angle = AngleType.High;
                                    }
                                }
                            }
                            catch
                            {

                            }
                        }

                        System.Threading.Thread.Sleep(10);
                    }
                });


                return true;
            }
            catch (Exception ex)
            {
                this.Log($"Failed connect COM{comPort}. EX: {ex}");
                this.Run = false;
                return false;
            }
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string lastWord = string.Empty;

        private PresageClient presage;
        private WristParser wrist;
        private ICommand typeCommand;

        private SpeechSynthesizer speechSynthesizer = new SpeechSynthesizer();

        public MainWindow()
        {
            InitializeComponent();
            this.wrist = new WristParser();
            this.typeCommand = new DelegateCommand()
            {
                OnExecute = (a) =>
                {
                    var prediction = a as string;
                    this.lastWord = prediction;
                    this.txtOutput.Text += lastWord + " ";
                    this.DoPredict();
                }
            };

            this.Closing += (o, e) => this.wrist.Run = false;
            this.presage = new PresageSvc.PresageClient();

            this.presage.learn("I need your help please.");
            this.presage.learn("Thank you.");
            this.presage.learn("Can you help me?");
            this.presage.learn("I feel hungry");
            this.presage.learn("I feel thirsty");
            this.presage.learn("I'm hungry");
            this.presage.learn("I'm thirsty");
            this.presage.learn("I'm in pain");
            this.presage.learn("I need help");
            this.presage.learn("I need your help");
            this.presage.learn("Help please");
            this.presage.learn("I need to go to the toilet");
            this.presage.learn("I need the bathroom");
            this.presage.learn("I need the toilet");
            this.presage.learn("I'm tired");
            this.presage.learn("I love you");
            this.presage.learn("How are you?");
            this.presage.learn("I want to watch TV");
            this.presage.learn("I want to watch television");
            this.presage.learn("How is work?");

            //presage.Open();

            this.wrist.FocusContainer += this.OnFocusContainer;
            this.wrist.GestureReceived += this.OnGestureReceived;
            this.wrist.GeneralMessageReceived += this.OnGeneralMessage;
            this.wrist.AngleChanged += this.OnAngleChanged;
            this.DataContext = this.wrist;
            this.Reset();
        }

        private void OnAngleChanged(AngleType newAngle, AngleType oldAngle)
        {
            this.Dispatcher.Invoke(() =>
            {
                this.txtDebug.Text += $"New Angle: {newAngle}, Old Angle: {oldAngle}";
                if(newAngle == AngleType.High && oldAngle == AngleType.MidHeight)
                {
                    this.ExecuteFocusedButton();
                }
            });
        }

        private void ExecuteFocusedButton()
        {
            foreach (var btn in this.btnContainer.Children.Cast<Button>())
            {
                if (btn.IsFocused)
                {
                    btn.Command.Execute(btn.CommandParameter);
                    return;
                }
            }
        }

        private void OnGeneralMessage(string obj)
        {
            this.Dispatcher.Invoke(() =>
            {
                this.txtDebug.Text += obj;
            });
        }

        private void OnGestureReceived(string gesture)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (gesture == "SHAKE")
                {
                    Task.Run(() =>
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            this.Reset();
                        });
                    });
                }
                else if(gesture == "LOGOUP")
                {
                    this.ExecuteFocusedButton();
                }
            });
        }

        private void OnFocusContainer(int index)
        {
            this.Dispatcher.Invoke(() =>
            {
                (this.btnContainer.Children[index] as Button).Focus();
            });
        }

        public void DoPredict()
        {
            var predictions = 
                string.IsNullOrWhiteSpace(this.txtOutput.Text)
                ? new[]
                {
                    "I",
                    "Hello",
                    "Can you",
                    "I need",
                    "Help",
                    "Thank you",
                }
                : this.presage.predict(
                this.txtOutput.Text, 
                string.Empty);
            this.btnContainer.Children.Clear();

            foreach(var prediction in predictions)
            {
                var btn = new Button()
                {
                    CommandParameter = prediction,
                    Content = prediction,
                    Command = this.typeCommand
                };

                this.btnContainer.Children.Add(btn);
            }

            this.wrist.NumContainers = this.btnContainer.Children.Count;
        }

        public void Reset()
        {
            this.speechSynthesizer.SelectVoiceByHints(VoiceGender.Female);
            this.speechSynthesizer.SpeakAsync(this.txtOutput.Text);

            this.txtOutput.Text = string.Empty;

            this.lastWord = string.Empty;
            this.DoPredict();
            this.wrist.StartListen(12);
        }
    }

    public class DelegateCommand : ICommand
    {
        public Action<object> OnExecute { get; set; }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => this.OnExecute?.Invoke(parameter);
    }
}
