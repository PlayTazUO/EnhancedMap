using System;
using System.Collections.Generic;
using EnhancedMap.Drawing;
using System.Linq;
using EnhancedMap.Core.MapObjects;

namespace EnhancedMap.Core
{
    public class OnMapChangedEventArgs : EventArgs
    {
        public OnMapChangedEventArgs(int prev, int current)
        {
            Previous = prev;
            Current = current;
            Allow = true;
        }

        public int Previous { get; }
        public int Current { get; }
        public bool Allow { get; set; }
    }


    public static class Global
    {
        private static int _facet;
        private static bool _freeview;
        private static int _zoomIndex = 4;
        private static readonly float[] _zooms = new float[10] {0.125f, 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 4f, 6f, 8f};

        private static int _firstValidFacet = -1;

        public static bool IsFacetChanged { get; set; }

        public static bool FreeView
        {
            get => _freeview;
            set
            {
                _freeview = value;
                OnFreeView.Raise(value);
            }
        }

        public static float Zoom => _zooms[_zoomIndex];

        public static string UOPath { get; set; }

        public static float Angle { get; set; } = 45;

        public static UserObject TrackedUser { get; set; }
        public static BuildingObject CurrentBuildObject { get; set; }
        public static HouseObject CurrentHouseObject { get; set; }
        public static LabelObject CurrentLabelObject { get; } = new LabelObject();

        // The main window is owned by the UI layer (EnhancedMap.Desktop). Kept as object to avoid a
        // reverse dependency from Core -> UI.
        public static object MainWindow { get; set; }

        public static MapEntry[] Maps { get; } = new MapEntry[6];

        public static float X { get; set; }
        public static float Y { get; set; }

        public static int Facet
        {
            get => _facet;
            set
            {
                var eventargs = new OnMapChangedEventArgs(_facet, value);
                FacetChanged.Raise(eventargs);
                // if (eventargs.Allow)
                _facet = value;
            }
        }

        public static int FirstValidFacet
        {
            get
            {
                if (_firstValidFacet == -1) _firstValidFacet = Maps.FirstOrDefault(s => s != null)?.Index ?? 0;
                return _firstValidFacet;
            }
        }

        public static List<string> HighlightesUsername { get; } = new List<string>();


        public static Dictionary<string, object> SettingsCollection { get; } = new Dictionary<string, object>
        {
            // network
            {"username", "You"},
            {"password", ""},
            {"remembercredentials", true},
#if BETA
            { "ip", "enhancedmap.hopto.org" },
#else
            {"ip", ""},
#endif
            {"port", 8887},
            {"rememberserver", true},
            {"autologin", false},
            {"tryreconnect", false},
            {"tryreconnecttime", 30},

            // general
            {"showhits", true},
            {"showstamina", true},
            {"showmana", true},
            {"centeruser", ""},
            {"trackdeathpoint", true},
            {"showhiddenicon", true},
            {"showdeathicon", true},
            {"abbreviatenames", false},
            {"panicsounds", true},
            {"alertsounds", true},
            {"smartnamesposition", false},
            {"showplacesicons", true},
            {"hidelessimportantplaces", false},
            {"showtownsnames", false},
            {"showserverbounds", true},
            {"showguardlines", true},
            {"showcoordinates", true},
            {"showhouses", true},
            {"showmobilesaroundyou", true},
            {"dontrenderusersindifferentfacets", false},
            {"nameposition", 0},
            {"namecolor", Color.White.ToArgb()},
            {"namefont", "Segoe UI"},
            {"namesize", 12},
            {"namestyle", (int) FontStyle.Regular},

            // application
            {"showscrollbarls", false},
            {"openchatatstart", false},
            {"showincomingmsg", false},
            {"soundsincomingmsg", true},
            {"followuowindowstate", false},
            {"loadmapsondemand", false},
            {"showfps", false},
            {"fps", 30},
            {"mapkind", 0},
            {"clientpath", string.Empty},
            {"mapX", -1},
            {"mapY", -1},
            {"mapW", -1},
            {"mapH", -1},
            {"chatX", -1},
            {"chatY", -1},
            {"chatW", -1},
            {"chatH", -1},
            {"zoomIndex", 4},
            {"chatfontsize", 10},

            // diagnostic
            {"autoscroll", true}
        };

        public static event EventHandler<bool> OnFreeView;
        public static event EventHandler<OnMapChangedEventArgs> FacetChanged;

        /// <summary>Display name used for outgoing chat/actions (there is no local player object).</summary>
        public static string SelfName => SettingsCollection["username"].ToString();

        /// <summary>Color used for outgoing chat/actions.</summary>
        public static Color SelfColor => Color.FromArgb(SettingsCollection["namecolor"].ToInt());

        /// <summary>
        ///     Centers the view on the given connected user and remembers the choice so it is
        ///     followed again on future sessions. Pass null to stop following anyone.
        /// </summary>
        public static void CenterOn(UserObject user)
        {
            FreeView = false;
            TrackedUser = user;
            SettingsCollection["centeruser"] = user?.Name ?? string.Empty;
        }

        /// <summary>Re-applies the remembered center user once it connects.</summary>
        public static void TryAutoCenter(UserObject user)
        {
            if (user == null)
                return;

            string remembered = SettingsCollection["centeruser"]?.ToString();
            if (!string.IsNullOrEmpty(remembered) && string.Equals(remembered, user.Name, StringComparison.OrdinalIgnoreCase))
            {
                TrackedUser = user;
                FreeView = false;
            }
        }

        /// <summary>
        ///     True when the given user is the one currently being followed (see <see cref="CenterOn" />).
        ///     Settings that relate to "our" character are scoped to the followed user, since there is
        ///     no local player object.
        /// </summary>
        public static bool IsTrackedUser(UserObject user)
        {
            return user != null && !user.IsDisposing && ReferenceEquals(user, TrackedUser);
        }


        public static void ChangeZoom(bool plus)
        {
            if (plus)
            {
                _zoomIndex--;
                if (_zoomIndex < 0)
                    _zoomIndex = 0;
            }
            else
            {
                _zoomIndex++;
                if (_zoomIndex > _zooms.Length - 1)
                    _zoomIndex = _zooms.Length - 1;
            }
        }

        public static int SetInitialZoom(float zoom)
        {
            for (_zoomIndex = 0; _zoomIndex < _zooms.Length; _zoomIndex++)
                if (zoom == _zooms[_zoomIndex])
                    return _zoomIndex;
            return _zoomIndex = 4;
        }

        public static void SetZoomIndex(int index)
        {
            if (index < 0 || index >= _zooms.Length)
                _zoomIndex = 4;
            else
                _zoomIndex = index;
        }
    }
}