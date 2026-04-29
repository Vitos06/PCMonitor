using LibreHardwareMonitor.Hardware;
using System;
using System.Windows;
using System.Windows.Threading;

namespace PCMonitor
{
    public partial class MainWindow : Window
    {
        private Computer computer;
        private DispatcherTimer timer;

        private string cpuName = "Unknown CPU";
        private string gpuName = "Unknown GPU";

        private int cpuThreads = 0;
        private int cpuCores = 0;
        private float gpuVram = 0;

        public MainWindow()
        {
            InitializeComponent();

            MouseLeftButtonDown += (s, e) => DragMove();

            InitHardware();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ===============================
        // СТАБИЛЬНАЯ ИНИЦИАЛИЗАЦИЯ
        // ===============================
        private void InitHardware()
        {
            // 1 попытка — CPU + GPU + RAM
            try
            {
                computer = new Computer()
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,

                    // отключаем нестабильные модули
                    IsMotherboardEnabled = false,
                    IsControllerEnabled = false
                };

                computer.Open();
                return;
            }
            catch
            {
            }

            // 2 попытка — CPU + RAM без GPU
            try
            {
                computer = new Computer()
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = false,
                    IsMemoryEnabled = true,

                    IsMotherboardEnabled = false,
                    IsControllerEnabled = false
                };

                computer.Open();
                gpuName = "GPU unavailable";
                return;
            }
            catch
            {
            }

            MessageBox.Show("Hardware sensors unavailable.");
        }

        // ===============================
        // ОСНОВНОЙ TIMER
        // ===============================
        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                float cpuTemp = 0;
                float cpuLoad = 0;

                float gpuTempCore = 0;
                float gpuTempHotspot = 0;
                float gpuLoad = 0;

                float gpuFanRpm = 0;
                float gpuFanPercent = 0;

                float ramUsed = 0;

                cpuThreads = Environment.ProcessorCount;
                cpuCores = 0;
                gpuVram = 0;

                if (computer == null)
                    return;

                foreach (IHardware hardware in computer.Hardware)
                {
                    try
                    {
                        hardware.Update();
                    }
                    catch
                    {
                        continue;
                    }

                    // ================= CPU =================
                    if (hardware.HardwareType == HardwareType.Cpu)
                        cpuName = hardware.Name;

                    // ================= GPU =================
                    if (hardware.HardwareType == HardwareType.GpuNvidia ||
                        hardware.HardwareType == HardwareType.GpuAmd)
                        gpuName = hardware.Name;

                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor == null || !sensor.Value.HasValue)
                            continue;

                        float val = sensor.Value.Value;
                        string name = sensor.Name.ToLower();

                        // ================= CPU =================
                        if (hardware.HardwareType == HardwareType.Cpu)
                        {
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                if (name.Contains("package"))
                                    cpuTemp = val;
                                else if (name.Contains("core"))
                                    cpuTemp = Math.Max(cpuTemp, val);
                            }

                            if (sensor.SensorType == SensorType.Load &&
                                sensor.Name == "CPU Total")
                                cpuLoad = val;

                            if (sensor.Name.Contains("Core #"))
                                cpuCores++;
                        }

                        // ================= GPU =================
                        if (hardware.HardwareType == HardwareType.GpuNvidia ||
                            hardware.HardwareType == HardwareType.GpuAmd)
                        {
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                if (name.Contains("hotspot"))
                                    gpuTempHotspot = val;

                                if (name.Contains("core") ||
                                    name == "temperature")
                                    gpuTempCore = val;
                            }

                            if (sensor.SensorType == SensorType.Load)
                                gpuLoad = Math.Max(gpuLoad, val);

                            if (sensor.SensorType == SensorType.Fan)
                                gpuFanRpm = Math.Max(gpuFanRpm, val);

                            if (sensor.SensorType == SensorType.Control)
                                gpuFanPercent = Math.Max(gpuFanPercent, val);

                            if (sensor.SensorType == SensorType.Data ||
                                sensor.SensorType == SensorType.SmallData)
                            {
                                if (name.Contains("memory") ||
                                    name.Contains("dedicated") ||
                                    name.Contains("d3d"))
                                {
                                    gpuVram = Math.Max(gpuVram, val);
                                }
                            }
                        }

                        // ================= RAM =================
                        if (hardware.HardwareType == HardwareType.Memory)
                        {
                            if (sensor.SensorType == SensorType.Data &&
                                name.Contains("used"))
                            {
                                ramUsed = val;
                            }
                        }
                    }
                }

                if (cpuCores == 0)
                    cpuCores = cpuThreads / 2;

                float gpuTemp = gpuTempCore > 0 ? gpuTempCore : gpuTempHotspot;

                // ================= UI =================
                CpuNameText.Text = cpuName;
                CpuSpecsText.Text = $"{cpuCores} cores / {cpuThreads} threads";
                CpuTempText.Text = $"Temp: {cpuTemp:F0} °C";
                CpuLoadBar.Value = cpuLoad;
                CpuLoadText.Text = $"Load: {cpuLoad:F0}%";

                GpuNameText.Text = gpuName;
                GpuSpecsText.Text = $"VRAM: {gpuVram:F0} MB";
                GpuTempText.Text = $"Temp: {gpuTemp:F0} °C";
                GpuLoadBar.Value = gpuLoad;
                GpuLoadText.Text = $"Load: {gpuLoad:F0}%";
                GpuFanText.Text = $"Fan: {gpuFanPercent:F0}% ({gpuFanRpm:F0} RPM)";

                RamText.Text = $"RAM: {ramUsed:F1} GB";
            }
            catch
            {
                // silent protection
            }
        }
    }
}