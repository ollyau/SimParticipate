using BeatlesBlog.SimConnect;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimParticipate
{
    public class OpenEventArgs : EventArgs
    {
        public string SimulatorName { get; private set; }
        public OpenEventArgs(string SimulatorName)
        {
            this.SimulatorName = SimulatorName;
        }
    }

    class SimConnectHelpers
    {
        public static bool IsLocalRunning
        {
            get { return LookupDefaultPortNumber("SimConnect_Port_IPv4") != 0 || LookupDefaultPortNumber("SimConnect_Port_IPv6") != 0; }
        }

        public static int LookupDefaultPortNumber(string strValueName)
        {
            string[] simulators = {
                                      @"HKEY_CURRENT_USER\Software\Microsoft\Microsoft Games\Flight Simulator",
                                      @"HKEY_CURRENT_USER\Software\Microsoft\Microsoft ESP",
                                      @"HKEY_CURRENT_USER\Software\LockheedMartin\Prepar3D",
                                      @"HKEY_CURRENT_USER\Software\Lockheed Martin\Prepar3D v2",
                                      @"HKEY_CURRENT_USER\Software\Lockheed Martin\Prepar3D v3",
                                      @"HKEY_CURRENT_USER\Software\Lockheed Martin\Prepar3D v4",
                                      @"HKEY_CURRENT_USER\Software\Microsoft\Microsoft Games\Flight Simulator - Steam Edition"
                                  };
            foreach (var sim in simulators)
            {
                var value = (string)Microsoft.Win32.Registry.GetValue(sim, strValueName, null);
                if (!string.IsNullOrEmpty(value))
                {
                    int port = int.Parse(value);
                    if (port != 0) { return port; }
                }
            }
            return 0;
        }
    }

    class SimEventManager
    {
        private SimConnect _sc;
        private Dictionary<string, uint> _events;
        private HashSet<string> _simEvents;

        public SimEventManager(SimConnect sc)
        {
            _sc = sc;
            _events = new Dictionary<string, uint>();
            if (File.Exists("events.json"))
            {
                var data = JObject.Parse(File.ReadAllText("events.json"));
                _simEvents = data["events"].ToObject<HashSet<string>>();
            }
        }

        private Events GetEventID(string strEventID)
        {
            var evt = strEventID.Trim().ToUpperInvariant();
            if (_simEvents != null && !_simEvents.Contains(evt))
            {
                throw new KeyNotFoundException($"Given event string '{strEventID}' does not exist in Flight Simulator X.");
            }
            uint value;
            var result = _events.TryGetValue(evt, out value);
            if (result)
            {
                return (Events)value;
            }
            else
            {
                value = (uint)_events.Count;
                _sc.MapClientEventToSimEvent((Events)value, evt);
                _events.Add(evt, value);
                return (Events)value;
            }
        }

        public int TransmitClientEventToUser(string strEventID, SIMCONNECT_GROUP_PRIORITY ePriority)
        {
            var eEventID = GetEventID(strEventID);
            return _sc.TransmitClientEventToUser(eEventID, ePriority);
        }

        public int TransmitClientEventToUser(string strEventID, uint dwData, SIMCONNECT_GROUP_PRIORITY ePriority)
        {
            var eEventID = GetEventID(strEventID);
            return _sc.TransmitClientEventToUser(eEventID, dwData, ePriority);
        }
    }

    class SimConnectInstance
    {
        private Log log;
        private SimConnect sc;
        private SimEventManager events;
        private const string appName = "SimParticipate";

        public EventHandler<OpenEventArgs> OpenEvent;
        public EventHandler DisconnectEvent;

        protected virtual void OnRaiseOpenEvent(OpenEventArgs e)
        {
            OpenEvent?.Invoke(this, e);
        }

        protected virtual void OnRaiseDisconnectEvent(EventArgs e)
        {
            DisconnectEvent?.Invoke(this, e);
        }

        public SimConnectInstance()
        {
            log = Log.Instance;
            sc = new SimConnect(null);

            sc.OnRecvOpen += OnRecvOpen;
            sc.OnRecvException += OnRecvException;
            sc.OnRecvQuit += OnRecvQuit;
        }

        public void Connect()
        {
            if (SimConnectHelpers.IsLocalRunning)
            {
                try
                {
                    log.Info("Opening SimConnect connection.");
                    sc.Open(appName);
                }
                catch (SimConnect.SimConnectException ex)
                {
                    log.Warning(string.Format("Local connection failed.\r\n{0}", ex.ToString()));
                    try
                    {
                        bool ipv6support = System.Net.Sockets.Socket.OSSupportsIPv6;
                        log.Info($"Opening SimConnect connection ({(ipv6support ? "IPv6" : "IPv4")}).");
                        int scPort = SimConnectHelpers.LookupDefaultPortNumber(ipv6support ? "SimConnect_Port_IPv6" : "SimConnect_Port_IPv4");
                        if (scPort == 0) { throw new SimConnect.SimConnectException("Invalid port."); }
                        sc.Open(appName, null, scPort, ipv6support);
                    }
                    catch (SimConnect.SimConnectException innerEx)
                    {
                        log.Error(string.Format("Local connection failed.\r\n{0}", innerEx.ToString()));
                    }
                }
            }
            else
            {
                log.Warning("Flight Simulator must be running in order to connect to SimConnect.");
            }
        }

        public void Disconnect()
        {
            log.Info("Closing SimConnect connection.");
            sc.Close();
            OnRaiseDisconnectEvent(EventArgs.Empty);
        }

        private void OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            var simVersion = $"{data.dwApplicationVersionMajor}.{data.dwApplicationVersionMinor}.{data.dwApplicationBuildMajor}.{data.dwApplicationBuildMinor}";
            var scVersion = $"{data.dwSimConnectVersionMajor}.{data.dwSimConnectVersionMinor}.{data.dwSimConnectBuildMajor}.{data.dwSimConnectBuildMinor}";
            log.Info($"Connected to {data.szApplicationName}\r\n    Simulator Version:\t{simVersion}\r\n    SimConnect Version:\t{scVersion}");
            OnRaiseOpenEvent(new OpenEventArgs(data.szApplicationName));

            // do stuff once connected to the simulator, like simconnect requests or whatever
            events = new SimEventManager(sender);
        }

        private void OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            var exceptionName = Enum.GetName(typeof(SIMCONNECT_EXCEPTION), data.dwException);
            log.Warning($"OnRecvException: {data.dwException} ({exceptionName}) {data.dwSendID} {data.dwIndex}");
            sc.Text(SIMCONNECT_TEXT_TYPE.PRINT_WHITE, 10.0f, Requests.DisplayText, $"{appName} SimConnect Exception: {data.dwException} ({exceptionName})");
        }

        private void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            log.Info("OnRecvQuit - simulator has closed.");
            Disconnect();
        }

        public int TransmitClientEvent(string evt)
        {
            return events.TransmitClientEventToUser(evt, SIMCONNECT_GROUP_PRIORITY.HIGHEST);
        }

        public int TransmitClientEvent(string evt, uint data)
        {
            return events.TransmitClientEventToUser(evt, data, SIMCONNECT_GROUP_PRIORITY.HIGHEST);
        }
    }
}