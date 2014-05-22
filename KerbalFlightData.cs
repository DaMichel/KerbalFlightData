/*
    Copyright 2014 DaMichel
 
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Text;
using UnityEngine;
using System.Reflection;

[KSPAddon(KSPAddon.Startup.Flight, false)]
public class DMFlightData : MonoBehaviour
{
    double machNumber;
    double airAvailability;
    double stallPercentage;
    double q;
    int    warnQ = 0;
    int    warnStall = 0;
    int    warnAir = 0;
    int    warnTemp = 0;
    double apoapsis = 0;
    double periapsis = 0;
    double altitude = 0;
    double timeToNode = 0;
    enum NextNode {
        Ap, Pe
    };
    NextNode nextNode = NextNode.Ap;
    double highestTemp = 0;
    double highestRelativeTemp = 0;
    bool   displayOrbitalData = false;
    bool   displayAtmosphericData = false;
    bool   farDataIsObtainedOkay = true;

    bool hasDRE   = false;
    Type FARControlSys = null;

    static Toolbar.IButton toolbarButton;

    #region Config & Data Acquisition
    void UpdateFARData()
    {
        var instance = FARControlSys.GetField("activeControlSys", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        foreach (var field in FARControlSys.GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.Name == "q")
                q = (double)field.GetValue(instance);
            else if (field.Name == "MachNumber")
                machNumber = (double)field.GetValue(instance);
            else if (field.Name == "intakeDeficit")
                airAvailability = (double)field.GetValue(null);
            else if (field.Name == "stallPercentage")
                stallPercentage = (double)field.GetValue(null);
        }
    }


    void Awake()
    {
        //Debug.Log("DMFlightData Awake");
        foreach (var assembly in AssemblyLoader.loadedAssemblies)
        {
            //Debug.Log(assembly.name);
            if (assembly.name == "FerramAerospaceResearch")
            {
                var types = assembly.assembly.GetExportedTypes(); 
                foreach (Type t in types)
                {
                    //Debug.Log(t.FullName);
                    if (t.FullName.Equals("ferram4.FARControlSys")) 
                    {
                        FARControlSys = t;
                    }
                }
            }
            if (assembly.name == "DeadlyReentry")
            {
                hasDRE = true;
            }
        }

        toolbarButton = Toolbar.ToolbarManager.Instance.add("KerbalFlightData", "damichelsflightdata");
        toolbarButton.TexturePath = "KerbalFlightData/toolbarbutton";
        toolbarButton.ToolTip = "KerbalFlightData On/Off Switch";
        toolbarButton.Visibility = new Toolbar.GameScenesVisibility(GameScenes.FLIGHT);
        //toolbarButton.Visible = true;
        toolbarButton.Enabled = true;
        toolbarButton.OnClick += (e) =>
        {
            enabled = !enabled;
        };
    }


    public void SaveSettings()
    {
        ConfigNode settings = new ConfigNode();
        settings.name = "SETTINGS";
        settings.AddValue("active", enabled);
        settings.Save(AssemblyLoader.loadedAssemblies.GetPathByType(typeof(DMFlightData)) + "/settings.cfg");
    }


    public void LoadSettings()
    {
        ConfigNode settings = new ConfigNode();
        settings = ConfigNode.Load(AssemblyLoader.loadedAssemblies.GetPathByType(typeof(DMFlightData)) + @"\settings.cfg".Replace('/', '\\'));

        if (settings != null)
        {
            if (settings.HasValue("active")) enabled = bool.Parse(settings.GetValue("active"));
        }
    }


	void Start()
    {
        LoadSettings();
    }


    public void OnDestroy()
    {
        SaveSettings();
    }


    void LateUpdate()
    {
        if (enabled == false) return;

        Vessel vessel = FlightGlobals.ActiveVessel;
        if (vessel == null) return;

        try
        {
            UpdateFARData();
            //machNumber      = ferram4.FARControlSys.activeMach;
            //airAvailability = ferram4.FARControlSys.intakeDeficit;
            //stallPercentage = ferram4.FARControlSys.stallPercentage;
            //q               = ferram4.FARControlSys.q;
            farDataIsObtainedOkay = true;
        }
        catch (Exception e)
        {
            if (farDataIsObtainedOkay) // if it was obtained okay the last time
            {
                Debug.Log("DMFlightData: "+e.ToString());
                farDataIsObtainedOkay = false;
            }
            // otherwise remain silent!
        }

        if (q < 10)
        {
            warnQ = 0;
            warnStall = 0;
        }
        else
        {
            if (q > 40000)
                warnQ = 1;
            else 
                warnQ = 0;

            if (stallPercentage > 0.5)
                warnStall = 2;
            else if (stallPercentage > 0.005)
                warnStall = 1;
            else
                warnStall = 0;
        }
        if (airAvailability < 1.05)
            warnAir = 2;
        else if (airAvailability < 1.5)
            warnAir = 1;
        else
            warnAir = 0;
        
        Orbit o = vessel.orbit;
        CelestialBody b = vessel.mainBody;

        displayAtmosphericData = farDataIsObtainedOkay;
        displayOrbitalData = false;
        if (o != null && b != null)
        {
            periapsis = o.PeA;
            apoapsis  = o.ApA;
            if (o.timeToAp < o.timeToPe)
            {
                timeToNode = o.timeToAp;
                nextNode   = NextNode.Ap;
            }
            else
            {
                timeToNode = o.timeToPe;
                nextNode   = NextNode.Pe;
            }

            if (b.atmosphere)
            {
                double hmin = (double)(((int)(b.maxAtmosphereAltitude * 0.33333333e-3)))*1000;
                displayOrbitalData = apoapsis>hmin || periapsis>hmin;
            }
            else
                displayOrbitalData = true;
   
            displayAtmosphericData &= b.atmosphere && vessel.altitude<b.maxAtmosphereAltitude;
        }
        altitude = vessel.altitude;

        if (hasDRE)
        {
            highestTemp = 0;
            highestRelativeTemp = 0;
            foreach (Part p in vessel.parts)
            {
                if (p.temperature != 0) // small gear box has p.temperature==0 - always! Bug? Who knows. Anyway i want to ignore it.
                {
                    highestTemp = Math.Max(p.temperature, highestTemp);
                    highestRelativeTemp = Math.Max(p.temperature/p.maxTemp, highestRelativeTemp);
                }
            }
            if (highestRelativeTemp > 0.95)
                warnTemp = 2;
            else if (highestRelativeTemp > 0.8)
                warnTemp = 1;
            else
                warnTemp = 0;
        }
    }
    #endregion

    #region GUIUtil
    protected static String FormatAltitude(double x)
    {
        double a = Math.Abs(x);
        if (a > 1.0e6)
        {
            x *= 1.0e-6;
            a *= 1.0e-6;
            return x.ToString(a < 10 ? "F2" : (a < 100 ? "F1" : "F0")) + " Mm";
        }
        else //if (a > 1.0e3)
        {
            x *= 1.0e-3;
            a *= 1.0e-3;
            return x.ToString(a < 10 ? "F2" : (a < 100 ? "F1" : "F0")) + " km";
        }
        //else 
        //{
        //    return x.ToString("F0") + " m";
        //}
    }

    protected static String FormatTime(double x_)
    {
        int x = (int)x_;
        int min = 60;
        int h = min * 60;
        int d = h * 24;
        int y = d * 365;
        String res = "";
        if (x > y)
        {
            res += " " + (x / y).ToString() + "y";
            x = x % y;
        }
        if (x > d)
        {
            res += " " + (x / d).ToString() + "d";
            x = x % d;
        }
        if (x > h)
        {
            res += " " + (x / h).ToString() + "h";
            x = x % h;
        }
        if (x > min)
        {
            res += (x / min).ToString() + "m";
            x = x % min;
        }
        res += " " + x.ToString() + "s";
        return res;
    }
    #endregion

    #region GUI
    GUIStyle[] styles = { null, null, null };
    GUIStyle style_label = null;
    bool guiReady = false;

    void SetupGUI()
    {
        var s = new GUIStyle();
        s.hover.textColor = s.active.textColor = s.normal.textColor = s.focused.textColor = s.onNormal.textColor = s.onFocused.textColor = s.onHover.textColor = s.onActive.textColor = Color.white;
        s.padding = new RectOffset(1, 1, 1, 1);
        s.fontStyle = FontStyle.Bold;
        style_label = s;
        s = new GUIStyle(s);
        s.hover.textColor = s.active.textColor = s.normal.textColor = s.focused.textColor = s.onNormal.textColor = s.onFocused.textColor = s.onHover.textColor = s.onActive.textColor = Color.grey;
        styles[0] = s;
        s = new GUIStyle(s);
        s.hover.textColor = s.active.textColor = s.normal.textColor = s.focused.textColor = s.onNormal.textColor = s.onFocused.textColor = s.onHover.textColor = s.onActive.textColor = Color.yellow;
        s.fontStyle = FontStyle.Bold;
        styles[1] = s;
        s = new GUIStyle(s);
        s.hover.textColor = s.active.textColor = s.normal.textColor = s.focused.textColor = s.onNormal.textColor = s.onFocused.textColor = s.onHover.textColor = s.onActive.textColor = Color.red;
        s.fontStyle = FontStyle.Bold;
        styles[2] = s;

        guiReady = true;
    }


    protected void OnGUI()
	{
        if (enabled == false) return;
        try
        {
            if (!FlightUIModeController.Instance.navBall.expanded || !FlightUIModeController.Instance.navBall.enabled) return;
            switch (CameraManager.Instance.currentCameraMode)
            {
                case CameraManager.CameraMode.IVA:
                    return;
            }
            if (FlightGlobals.ActiveVessel.isEVA) return;
        }
        catch (NullReferenceException)
        {
            return;
        }

        if (!guiReady) SetupGUI();

        GUIStyle style = new GUIStyle();
        float height = 100f;
        float width  = 100f;
        float pos_x = Screen.width * 0.5f + 114f;
        float pos_y = Screen.height - height;
        GUI.Window(
            GUIUtility.GetControlID(FocusType.Passive),
            new Rect(pos_x, pos_y, width, height),
            this.DrawWindow1,
            GUIContent.none,
            style
        );

        width = 100f;
        pos_x = Screen.width * 0.5f - 114f - width;
        GUI.Window(
            GUIUtility.GetControlID(FocusType.Passive),
            new Rect(pos_x, pos_y, width, height),
            this.DrawWindow2,
            GUIContent.none,
            style
        );
    }

    protected void DrawWindow1(int windowId)
    {
        GUILayoutOption opt = GUILayout.ExpandWidth(true);
        GUILayout.BeginVertical();
        if (displayAtmosphericData)
        {
            GUILayout.Label("Mach " + machNumber.ToString("F2"), style_label, opt);
            GUILayout.Label("Alt  " + FormatAltitude(altitude), style_label, opt);
            String intakeLabel = "Intake";
            if (airAvailability < 2d) intakeLabel += " "+(airAvailability*100d).ToString("F0") + "%";
            GUILayout.Label(intakeLabel, styles[warnAir], opt);
            GUILayout.Label("Q", styles[warnQ], opt);
            GUILayout.Label("Stall", styles[warnStall], opt);
        }
        GUILayout.EndVertical();
    }

    protected void DrawWindow2(int windowId)
    {
        GUILayoutOption opt = GUILayout.ExpandWidth(true);
        GUILayout.BeginVertical();
        if (displayOrbitalData)
        {   
            String timeLabel = "";
            switch (nextNode)
            {
                case NextNode.Ap: timeLabel = "T_Ap- "; break;
                case NextNode.Pe: timeLabel = "T_Pe- "; break;
            }
            GUILayout.Label(timeLabel + FormatTime(timeToNode), style_label, opt);
            GUILayout.Label("Ap " + FormatAltitude(apoapsis), style_label, opt);
            GUILayout.Label("Pe " + FormatAltitude(periapsis), style_label, opt);
        }
        if (hasDRE)
        {
            GUILayout.Label("T " + highestTemp.ToString("F0") + " °C", styles[warnTemp], opt);
        }
        GUILayout.EndVertical();
    }
    #endregion
}
