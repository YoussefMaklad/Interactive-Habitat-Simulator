using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Media;
using Newtonsoft.Json;
using System.Runtime.InteropServices.ComTypes;

public class Animal
{
    public string Name { get; set; }
    public string ImagePath { get; set; }
    public string NameAudioFilePath { get; set; }
    public string DescriptionAudioFilePath { get; set; }
    public string HabitatAudioFilePath { get; set; }
    public string SoundAudioFilePath { get; set; }
}

public class Habitat
{
    public bool isActive = false;
    public string Name { get; set; }
    public string LightBackgroundPath { get; set; }
    public string DarkBackgroundPath { get; set; }
    public string FocusedImagePathInStartMenu { get; set; }
    public List<Animal> Animals { get; set; } = new List<Animal>();
}

public class CircularMenu
{
    public Point Center { get; set; }
    public int Radius { get; set; } = 100;

    private List<(string Label, Point Position)> menuItems;
    private int focusedItemIndex = -1;

    public Action<string> OnMenuItemChosen { get; set; }

    public CircularMenu(Point center)
    {
        Center = center;
        InitializeMenuItems();
    }

    private void InitializeMenuItems()
    {
        menuItems = new List<(string Label, Point Position)>
        {
            ("Name", new Point(Center.X, Center.Y - Radius)), // Top
            ("Description", new Point(Center.X + Radius, Center.Y)), // Right
            ("Sound", new Point(Center.X, Center.Y + Radius)), // Bottom
            ("Habitat", new Point(Center.X - Radius, Center.Y)) // Left
        };
    }

    public void Rotate()
    {
        if (focusedItemIndex == -1)
            focusedItemIndex = 0;
        else
            focusedItemIndex = (focusedItemIndex + 1) % menuItems.Count;
    }

    public void Select()
    {
        if (focusedItemIndex >= 0 && focusedItemIndex < menuItems.Count)
        {
            OnMenuItemChosen?.Invoke(menuItems[focusedItemIndex].Label);
        }
    }

    public void Draw(Graphics g)
    {
        using (Pen pen = new Pen(Color.White, 2))
        using (SolidBrush brush = new SolidBrush(Color.LightGray))
        using (Font font = new Font("Arial", 10))
        {
            for (int i = 0; i < menuItems.Count; i++)
            {
                var (label, position) = menuItems[i];
                bool isFocused = (i == focusedItemIndex);

                Color fillColor = isFocused ? Color.Yellow : Color.LightGray;
                g.FillEllipse(new SolidBrush(fillColor), position.X - 50, position.Y - 50, 100, 100);
                g.DrawEllipse(pen, position.X - 50, position.Y - 50, 100, 100);

                // Draw the label
                SizeF labelSize = g.MeasureString(label, font);
                g.DrawString(label, font, Brushes.Black,
                             position.X - labelSize.Width / 2,
                             position.Y - labelSize.Height / 2);
            }
        }
    }
}

public enum UserIdentity
{
    TEACHER,
    KID,
    NONE
}

public class IdentityManager
{
    private UserIdentity _currentIdentity = UserIdentity.NONE;
    private bool _isIdentitySet = false;

    public UserIdentity CurrentIdentity
    {
        get => _currentIdentity;
        private set
        {
            _currentIdentity = value;
            _isIdentitySet = _currentIdentity != UserIdentity.NONE;
        }
    }

    public bool IsIdentitySet => _isIdentitySet;

    public void SetIdentity(UserIdentity identity)
    {
        CurrentIdentity = identity;
    }

    public void ClearIdentity()
    {
        CurrentIdentity = UserIdentity.NONE;
    }
}

public enum Theme
{
    LIGHT,
    DARK
}

namespace HabitatSimulator
{
    public partial class Form1 : Form
    {
        private bool showInitialImage = true;
        private bool showErrorImage = false;

        private Theme theme;
        private List<Habitat> habitats;
        private Animal currentAnimal = null;
        private Habitat currentHabitat = null;
        private CircularMenu circularMenu = null;
        private IdentityManager identityManager = new IdentityManager();
        private Dictionary<string, string> teacherReport = new Dictionary<string, string>();
        private Dictionary<string, string> onScreenTeacherReport = new Dictionary<string, string>();

        private string kidsReportType = "";
        private string chosenHabitat = "";
        private string chosenAnimal = "";
        private string chosenGesture = "";
        private bool showFocusedImageForChosenHabitat = false;
        private bool workingWithRotation = false;
        private bool HabitatActivation = false;
        private int counterForPlaySoundCorrect = 0;
        private int counterForPlaySoundWrong = 0;

        private TcpClient tcp_client;
        private NetworkStream _stream;
        private Thread _receiveThread;

        public Form1()
        {
            InitializeComponent();
            InitializeWindow();
            InitializeEventHandlers();
            InitializeTheme();
            InitializeUserIdentity();
            InitializeHabitatsAndAnimals();
        }

        private void InitializeTheme()
        {
            int currentHour = DateTime.Now.Hour;
            theme = (currentHour >= 6 && currentHour < 17) ? Theme.LIGHT : Theme.DARK;
        }

        private void InitializeUserIdentity()
        {
            identityManager.SetIdentity(UserIdentity.NONE);
        }

        private void InitializeWindow()
        {
            this.Name = "Interactive-Habitat-Simulator";
            this.Text = "Interactive-Habitat-Simulator";
            this.WindowState = FormWindowState.Maximized;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer, true);
        }

        private void InitializeEventHandlers()
        {
            this.FormClosed += Form1_FormClosed;
            this.Load += Form1_Load;
        }

        private void InitializeHabitatsAndAnimals()
        {
            habitats = new List<Habitat>
            {
                new Habitat
                {
                    Name = "Home",
                    LightBackgroundPath = "home.jpg",
                    DarkBackgroundPath = "HomeNight.png",
                    FocusedImagePathInStartMenu = "c-home.jpg",
                    Animals = new List<Animal>
                    {
                        new Animal { Name = "dog",  ImagePath = "dog.png", NameAudioFilePath = "sounds/dog-name.wav", DescriptionAudioFilePath = "sounds/dog-description.wav", HabitatAudioFilePath = "sounds/dog-habitat.wav", SoundAudioFilePath = "sounds/dog-sound.wav" },
                        new Animal { Name = "cat",  ImagePath = "cat.png", NameAudioFilePath = "sounds/cat-name.wav", DescriptionAudioFilePath = "sounds/cat-description.wav", HabitatAudioFilePath = "sounds/cat-habitat.wav", SoundAudioFilePath = "sounds/cat-sound.wav"},
                        new Animal { Name = "bird", ImagePath = "bird.png", NameAudioFilePath = "sounds/bird-name.wav", DescriptionAudioFilePath = "sounds/bird-description.wav", HabitatAudioFilePath = "sounds/bird-habitat.wav", SoundAudioFilePath = "sounds/bird-sound.wav"}
                    }
                },
                new Habitat
                {
                    Name = "WildLife",
                    LightBackgroundPath = "wildlife.jpg",
                    DarkBackgroundPath = "WildNight.png",
                    FocusedImagePathInStartMenu = "c-wildlife.jpg",
                    Animals = new List<Animal>
                    {
                        new Animal { Name = "bear", ImagePath = "bear.png", NameAudioFilePath = "sounds/bear-name.wav", DescriptionAudioFilePath = "sounds/bear-description.wav", HabitatAudioFilePath = "sounds/bear-habitat.wav", SoundAudioFilePath = "sounds/bear-sound.wav" },
                        new Animal { Name = "giraffe", ImagePath = "giraffe.png", NameAudioFilePath = "sounds/giraffe-name.wav", DescriptionAudioFilePath = "sounds/giraffe-description.wav", HabitatAudioFilePath = "sounds/giraffe-habitat.wav", SoundAudioFilePath = "sounds/giraffe-sound.wav"},
                        new Animal { Name = "elephant", ImagePath = "elephant.png", NameAudioFilePath = "sounds/elephant-name.wav", DescriptionAudioFilePath = "sounds/elephant-description.wav", HabitatAudioFilePath = "sounds/elephant-habitat.wav", SoundAudioFilePath = "sounds/elephant-sound.wav"},
                        new Animal { Name = "zebra", ImagePath = "zebra.png", NameAudioFilePath = "sounds/zebra-name.wav", DescriptionAudioFilePath = "sounds/zebra-description.wav", HabitatAudioFilePath = "sounds/zebra-habitat.wav", SoundAudioFilePath = "sounds/zebra-sound.wav"}
                    }
                },
                new Habitat
                {
                    Name = "Farm",
                    LightBackgroundPath = "farm.jpg",
                    DarkBackgroundPath = "FarmNight.png",
                    FocusedImagePathInStartMenu = "c-farm.jpg",
                    Animals = new List<Animal>
                    {
                        new Animal { Name = "sheep", ImagePath = "sheep.png", NameAudioFilePath = "sounds/sheep-name.wav", DescriptionAudioFilePath = "sounds/sheep-description.wav", HabitatAudioFilePath = "sounds/sheep-habitat.wav", SoundAudioFilePath = "sounds/sheep-sound.wav" },
                        new Animal { Name = "cow", ImagePath = "cow.png", NameAudioFilePath = "sounds/cow-name.wav", DescriptionAudioFilePath = "sounds/cow-description.wav", HabitatAudioFilePath = "sounds/cow-habitat.wav", SoundAudioFilePath = "sounds/cow-sound.wav"},
                        new Animal { Name = "horse", ImagePath = "horse.png", NameAudioFilePath = "sounds/horse-name.wav", DescriptionAudioFilePath = "sounds/horse-description.wav", HabitatAudioFilePath = "sounds/horse-habitat.wav", SoundAudioFilePath = "sounds/horse-sound.wav"}
                    }
                }
            };
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ConnectToPythonSocketServer();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                _receiveThread?.Abort();
                _stream?.Close();
                tcp_client?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during cleanup: {ex.Message}", "Cleanup Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ConnectToPythonSocketServer()
        {
            try
            {
                tcp_client = new TcpClient("127.0.0.1", 5000);
                _stream = tcp_client.GetStream();

                _receiveThread = new Thread(ReceiveMessagesFromPythonSocketServer);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();

                //MessageBox.Show("Connected to the Python server.", "Connection Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReceiveMessagesFromPythonSocketServer()
        {
            try
            {
                using (StreamReader reader = new StreamReader(_stream, Encoding.UTF8))
                {
                    while (true)
                    {
                        string message = reader.ReadLine();
                        if (message != null)
                        {
                            if (message.Contains("Habitat") && currentHabitat == null)
                            {

                                if (!HabitatActivation)
                                {
                                    HabitatActivation = true;
                                    chosenHabitat = message.Substring(8);
                                    if (showInitialImage)
                                    {
                                        showInitialImage = false;
                                        showFocusedImageForChosenHabitat = true;
                                    }
                                }
                            }
                            else if (message.Contains("Gesture"))
                            {
                                chosenGesture = message.Substring(8);
                                if (chosenGesture.Contains("Back") && currentHabitat != null && currentAnimal != null)
                                {
                                    ResetToInitialState();
                                    Invalidate();
                                    Update();
                                }
                                else if (workingWithRotation)
                                {
                                    if (chosenGesture == "Rotate")
                                    {
                                        circularMenu?.Rotate();
                                    }
                                    else if (chosenGesture == "Select")
                                    {
                                        circularMenu?.Select();
                                    }
                                }
                            }
                            else if (message.Contains("Animal"))
                            {
                                string animalName = message.Substring(7);
                                if (currentHabitat != null && (currentAnimal == null || EnsureAnimalInCurrentHabitat(animalName)))
                                {
                                    if (EnsureAnimalInCurrentHabitat(animalName))
                                    {
                                        chosenAnimal = animalName;
                                        if(counterForPlaySoundCorrect == 0)
                                        {
                                            string correctChoiceAudioPath = Path.Combine(Environment.CurrentDirectory, "sounds\\correct-choice.wav");
                                            PlaySound(correctChoiceAudioPath);
                                            counterForPlaySoundCorrect++;
                                        }
                                    }
                                    //else
                                    //{
                                    //    showErrorImage = true;
                                    //}
                                    else
                                    {
                                        if (counterForPlaySoundWrong == 0)
                                        {
                                            string wrongChoiceAudioPath = Path.Combine(Environment.CurrentDirectory, "sounds\\wrong-choice.wav");
                                            PlaySound(wrongChoiceAudioPath);
                                            counterForPlaySoundWrong++;
                                        }
                                        showErrorImage = true;
                                    }
                                }
                            }
                            else if (message.Contains("Identity") && !identityManager.Equals(UserIdentity.NONE))
                            {
                                string identity = message.Substring(9);
                                if (identity == "Kid")
                                    identityManager.SetIdentity(UserIdentity.KID);
                                else
                                {
                                    identityManager.SetIdentity(UserIdentity.TEACHER);
                                }
                                //ShowMessageBox("recived identity: " + identity);
                            }
                            else if (message.Contains("TeacherReport"))
                            {
                                string jsonPayload = message.Replace("TeacherReport:", "").Trim();
                                teacherReport = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonPayload);
                            }
                            else if (message.Contains("ReportType"))
                            {
                                kidsReportType = message.Replace("ReportType:", "").Trim();

                                onScreenTeacherReport.Clear();
                                foreach (var entry in teacherReport)
                                {
                                    string name = entry.Key;
                                    string mood = entry.Value.ToLower();
                                    if ((kidsReportType == "HappyKids" && mood == "happy") ||
                                        (kidsReportType == "SadKids" && mood == "sad") ||
                                        (kidsReportType == "NeutralKids" && mood == "neutral") ||
                                        (kidsReportType == "AngryKids" && mood == "angry") ||
                                        (kidsReportType == "FearKids" && mood == "fear"))
                                    {
                                        onScreenTeacherReport[name] = mood;
                                    }
                                }
                                Invalidate();
                            }

                            Invalidate();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //ShowMessageBox($"Error receiving data: {ex.Message}");
            }
        }

        private void ShowMessageBox(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => MessageBox.Show(message, "Message from Server", MessageBoxButtons.OK, MessageBoxIcon.Information)));
            }
            else
            {
                MessageBox.Show(message, "Message from Server", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private bool EnsureAnimalInCurrentHabitat(string animalName)
        {
            foreach (Animal animal in currentHabitat?.Animals)
            {
                if (animal.Name == animalName)
                    return true;
            }
            return false;
        }

        private void DisplayTeacherReport(Graphics g)
        {
            if (onScreenTeacherReport == null || onScreenTeacherReport.Count == 0)
                return;

            int midX = this.ClientSize.Width / 2;
            int topPadding = 300;
            int itemHeight = 40;
            int x = midX - 130;
            int y = topPadding + itemHeight;

            Font font = new Font("Arial", 40, FontStyle.Bold);
            Brush happyBrush = Brushes.Green;
            Brush sadBrush = Brushes.Red;
            Brush angryBrush = Brushes.Red;
            Brush neutralBrush = Brushes.Yellow;
            Brush fearBrush = Brushes.Blue;
            Brush currentBrush = null;

            if (kidsReportType == "HappyKids") { g.DrawString("Happy Children", font, happyBrush, new Point(x, topPadding)); currentBrush = happyBrush; }
            else if (kidsReportType == "SadKids") { g.DrawString("Sad Children", font, sadBrush, new Point(x, topPadding)); currentBrush = sadBrush; }
            else if (kidsReportType == "AngryKids") { g.DrawString("Angry Children", font, angryBrush, new Point(x, topPadding)); currentBrush = angryBrush; }
            else if (kidsReportType == "NeutralKids") { g.DrawString("Neutral Children", font, neutralBrush, new Point(x, topPadding)); currentBrush = neutralBrush; }
            else if (kidsReportType == "FearKids") { g.DrawString("Scared Children", font, fearBrush, new Point(x, topPadding)); currentBrush = fearBrush; }


            foreach (var entry in onScreenTeacherReport)
            {
                string kidName = entry.Key;
                g.DrawString(kidName, font, currentBrush, new Point(x, y));
                y += itemHeight;
            }
        }


        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            DrawScene(g);
        }

        private async void DrawScene(Graphics g)
        {
            g.Clear(Color.White);

            if (identityManager.CurrentIdentity == UserIdentity.KID)
            {
                if (showInitialImage)
                {
                    string initialImagePath = Path.Combine(Environment.CurrentDirectory, "select-habitat.jpg");
                    DrawBackgroundImage(g, initialImagePath);
                }

                if (showErrorImage)
                {
                    string errorImagePath = Path.Combine(Environment.CurrentDirectory, "error.png");
                    DrawBackgroundImage(g, errorImagePath);

                    await Task.Delay(1500);

                    showErrorImage = false;
                    Invalidate();
                    return;
                }
                else
                {
                    currentHabitat = habitats.FirstOrDefault(habitat => chosenHabitat.IndexOf(habitat.Name, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (currentHabitat != null)
                    {
                        currentHabitat.isActive = true;
                        workingWithRotation = true;

                        if (showFocusedImageForChosenHabitat)
                        {
                            string focusedImagePath = Path.Combine(Environment.CurrentDirectory, currentHabitat.FocusedImagePathInStartMenu);
                            DrawBackgroundImage(g, focusedImagePath);

                            await Task.Delay(2000);

                            showFocusedImageForChosenHabitat = false;
                            Invalidate();
                            return;
                        }

                        string currentHabitatBackgroundPath = "";

                        if (theme == Theme.LIGHT) currentHabitatBackgroundPath = Path.Combine(Environment.CurrentDirectory, currentHabitat.LightBackgroundPath);
                        else currentHabitatBackgroundPath = Path.Combine(Environment.CurrentDirectory, currentHabitat.DarkBackgroundPath);

                        DrawBackgroundImage(g, currentHabitatBackgroundPath);

                        if (!string.IsNullOrEmpty(chosenAnimal))
                            currentAnimal = currentHabitat.Animals.FirstOrDefault(animal =>
                                            animal.Name.Equals(chosenAnimal, StringComparison.OrdinalIgnoreCase));

                        if (currentAnimal != null)
                        {
                            string currentAnimalImagePath = Path.Combine(Environment.CurrentDirectory, currentAnimal.ImagePath);
                            Image img = Image.FromFile(currentAnimalImagePath);

                            DrawAnimalImage(g, currentAnimalImagePath, new Point(this.ClientSize.Width / 4, this.ClientSize.Height - img.Height));
                            if (circularMenu == null)
                            {
                                circularMenu = new CircularMenu(new Point(3 * this.ClientSize.Width / 4, this.ClientSize.Height / 4))
                                {
                                    OnMenuItemChosen = HandleMenuItemChosen
                                };
                            }

                            circularMenu.Draw(g);
                        }
                    }
                }
            }
            else if (identityManager.CurrentIdentity == UserIdentity.TEACHER)
            {
                string teacherImagePath = Path.Combine(Environment.CurrentDirectory, "teacher-report-animals.png");
                DrawBackgroundImage(g, teacherImagePath);
                DisplayTeacherReport(g);
                Invalidate();
                Update();
            }
            else
            {
                string authenticateImagePath = Path.Combine(Environment.CurrentDirectory, "AUTH.png");
                DrawBackgroundImage(g, authenticateImagePath);
            }
            Invalidate();
        }

        private void HandleMenuItemChosen(string label)
        {
            switch (label)
            {
                case "Name":
                    PlaySound(currentAnimal.NameAudioFilePath);
                    break;
                case "Description":
                    PlaySound(currentAnimal.DescriptionAudioFilePath);
                    break;
                case "Sound":
                    PlaySound(currentAnimal.SoundAudioFilePath);
                    break;
                case "Habitat":
                    PlaySound(currentAnimal.HabitatAudioFilePath);
                    break;
            }
        }

        private void ResetToInitialState()
        {
            chosenHabitat = "";
            chosenAnimal = "";
            currentHabitat = null;
            currentAnimal = null;
            circularMenu = null;

            showInitialImage = true;
            showErrorImage = false;
            workingWithRotation = false;
            HabitatActivation = false;

            showFocusedImageForChosenHabitat = false;
            counterForPlaySoundCorrect = 0;
            counterForPlaySoundWrong = 0;

            foreach (Habitat habitat in habitats)
            {
                habitat.isActive = false;
            }

            Invalidate();
        }

        private void DrawBackgroundImage(Graphics g, string imagePath)
        {
            using (Image image = Image.FromFile(imagePath))
            {
                g.DrawImage(image, new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height));
            }
        }

        private void DrawAnimalImage(Graphics g, string animalFileName, Point position)
        {
            string animalPath = Path.Combine(Environment.CurrentDirectory, animalFileName);
            if (File.Exists(animalPath))
            {
                using (Image animalImage = Image.FromFile(animalPath))
                {
                    int size = 343;
                    g.DrawImage(animalImage, new Rectangle(position.X - size / 2, position.Y, size, size));
                }
            }
        }

        private void PlaySound(string audioFilePath)
        {
            try
            {
                using (var player = new SoundPlayer(audioFilePath))
                {
                    player.Play();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing sound: {ex.Message}");
            }
        }
    }
}
