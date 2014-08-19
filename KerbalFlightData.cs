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
using System.Linq;
using System.Collections.Generic;

namespace KerbalFlightData
{

    public class DMDebug
    {
#if DEBUG
        StringBuilder sb = new StringBuilder();
        //Dictionary visited = new Dictionary<UnityEngine.Object, String>();
        HashSet<int> visited = new HashSet<int>(); //UnityEngine.Object

        bool CheckAndAddVisited(UnityEngine.Object o)
        {
            int key = o.GetInstanceID();
            if (visited.Contains(key)) return true;
            visited.Add(key);
            return false;
        }

        public void Out(String s, int indent)
        {
            var indentStr = new String(' ', indent);
            var arr = s.Split('\n');
            foreach (String s_ in arr)
            {
                String tmp = s_.Trim('\n', '\r', ' ', '\t').Trim();
                if (tmp.Length == 0) continue;
                sb.AppendLine(indentStr + tmp);
            }
        }

        bool IsInterestingType(Type typeToCheck)
        {
            var types = new Type[] {
            typeof(UnityEngine.Component),
            typeof(UnityEngine.GameObject),
            typeof(UnityEngine.Renderer),
            typeof(UnityEngine.Mesh),
            typeof(UnityEngine.Material),
            typeof(UnityEngine.Texture)
        };
            foreach (Type t in types)
            {
                if (t.IsAssignableFrom(typeToCheck)) return true;
            }
            return false;
        }

        bool IsOkayToExpand(string name, Type type)
        {
            if (!IsInterestingType(type)) return false;
            if (name == "parent" || name == "root" || name == "target") return false;
            return true;
        }

        public void PrintGameObjectHierarchy(GameObject o, int indent)
        {
            Out(o.name + ", lp = " + o.transform.localPosition.ToString("F3") + ", p = " + o.transform.position.ToString("F3"), indent);
            foreach (Transform t in o.transform)
            {
                PrintGameObjectHierarchy(t.gameObject, indent + 2);
            }
        }

        public void PrintGameObjectHierarchUp(GameObject o, out int indent)
        {
            if (o.transform.parent)
                PrintGameObjectHierarchUp(o.transform.parent.gameObject, out indent);
            else
                indent = 0;
            indent += 2;
            Out(o.name + ", lp = " + o.transform.localPosition.ToString("F3") + ", p = " + o.transform.position.ToString("F3"), indent);
        }

        public void PrintHierarchy(UnityEngine.Object instance, int indent = 0)
        {
            try
            {
                if (instance == null || CheckAndAddVisited(instance)) return;

                var t = instance.GetType();
                Out("+ " + instance.name + "(" + t.Name + ")", indent); //<" + instance.GetInstanceID() + ">"

                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    var value = field.GetValue(instance);
                    Out(field.FieldType.Name + " " + field.Name + " = " + value, indent + 1);
                    if (IsOkayToExpand(field.Name, field.FieldType))
                    {
                        PrintHierarchy((UnityEngine.Object)value, indent + 2);
                    }
                }
                foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    object value = null;
                    try
                    {
                        value = prop.GetValue(instance, null);
                    }
                    catch (Exception e)
                    {
                        value = e.ToString();
                    }
                    Out(prop.PropertyType.Name + " " + prop.Name + " = " + value, indent + 1);
                    if (IsOkayToExpand(prop.Name, prop.PropertyType))
                    {
                        PrintHierarchy((UnityEngine.Object)value, indent + 2);
                    }
                }

                if (typeof(GameObject).IsAssignableFrom(t))
                {
                    Out("[Components of " + instance.name + " ]", indent + 1);
                    foreach (var comp in ((GameObject)instance).GetComponents<Component>())
                    {
                        PrintHierarchy(comp, indent + 2);
                    }
                }
            }
            catch (Exception e)
            {
                Out("Error: " + e.ToString(), indent);
            }
        }

        public String ToString()
        {
            return sb.ToString();
        }
#endif

        public static void Log(string s)
        {
            Debug.Log("DMFlightData: " + s);
        }

        public static void LogWarning(string s)
        {
            Debug.LogWarning("DMFlightData: " + s);
        }
    }



    public static class Util
    {
        public static void SetColor(this GUIStyle s, Color c)
        {
            s.hover.textColor = s.active.textColor = s.normal.textColor = s.focused.textColor = s.onNormal.textColor = s.onFocused.textColor = s.onHover.textColor = s.onActive.textColor = c;
        }

        public static Vector2 ScreenSizeToWorldSize(Camera cam, Vector2 s)
        {
            Vector3 p0 = cam.ScreenToWorldPoint(Vector3.zero);
            Vector3 p1 = cam.ScreenToWorldPoint(s);
            return p1-p0;
        }
    };



    public static class MyStyleId
    {
        public const int Plain = 0;
        public const int Greyed = 1;
        public const int Warn1 = 2;
        public const int Warn2 = 3;
        public const int Emph = 4;
    };

    #region DataAcquisition
    public class Data
    {
        public double machNumber;
        public double airAvailability;
        public double stallPercentage;
        public double q;
        public bool hasAirAvailability = false;
        public bool hasAerodynamics = false; // mach, stall, q

        public int warnQ;
        public int warnStall;
        public int warnAir;
        public int warnTemp;

        public double apoapsis = 0;
        public double periapsis = 0;
        public double timeToNode = 0;
        public enum NextNode
        {
            Ap, Pe, Escape, Maneuver, Encounter
        };
        public NextNode nextNode = NextNode.Ap;

        public bool isAtmosphericLowLevelFlight;
        public bool isInAtmosphere;
        public bool isLanded;

        public double altitude = 0;
        public double radarAltitude = 0;

        public double highestTemp = 0;
        public double highestRelativeTemp = 0;
        public bool hasTemp = false;
    };


    abstract class DataSource
    {
        public abstract void Update(Data data, Vessel vessel);
    };


    class DataFAR : DataSource
    {
        private Type FARControlSys = null;
        private bool farDataIsObtainedOkay = true;

        public DataFAR(Type FARControlSys_)
            : base()
        {
            FARControlSys = FARControlSys_;
        }

        void GetFARData(Data data)
        {
            var instance = FARControlSys.GetField("activeControlSys", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            foreach (var field in FARControlSys.GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.Name == "q")
                    data.q = (double)field.GetValue(instance);
                else if (field.Name == "MachNumber")
                    data.machNumber = (double)field.GetValue(instance);
                else if (field.Name == "intakeDeficit")
                    data.airAvailability = (double)field.GetValue(null);
                else if (field.Name == "stallPercentage")
                    data.stallPercentage = (double)field.GetValue(null);
            }
            data.hasAerodynamics = true;
            data.hasAirAvailability = true;
        }

        public override void Update(Data data, Vessel vessel)
        {
            try
            {
                GetFARData(data);
                if (!farDataIsObtainedOkay)
                    DMDebug.Log("Data from FAR obtained successfully");
                farDataIsObtainedOkay = true;
            }
            catch (Exception e)
            {
                if (farDataIsObtainedOkay) // if it was obtained okay the last time
                {
                    DMDebug.Log(e.ToString());
                    farDataIsObtainedOkay = false;
                }
                // otherwise remain silent!
                // ... but set error flags
                data.hasAerodynamics = false;
                data.hasAirAvailability = false;
            }
        }
    };


    class DataIntakeAirStock : DataSource
    {
        public override void Update(Data data, Vessel vessel)
        {
            data.hasAirAvailability = data.isInAtmosphere;
            if (!data.hasAirAvailability) return;

            double airDemand = 0;
            double airAvailable = 0;

            double fixedDeltaTime = TimeWarp.fixedDeltaTime;
            PartResourceLibrary l = PartResourceLibrary.Instance;
            foreach (Part p in vessel.parts)
            {
                if (p == null)
                    continue;
                foreach (PartModule m in p.Modules)
                {
                    if (m is ModuleEngines)
                    {
                        ModuleEngines e = m as ModuleEngines;
                        if (e.EngineIgnited && !e.engineShutdown)
                        {
                            foreach (Propellant v in e.propellants)
                            {
                                string propName = v.name;
                                PartResourceDefinition r = l.resourceDefinitions[propName];
                                if (propName == "IntakeAir")
                                {
                                    airDemand += v.currentRequirement;
                                    continue;
                                }
                            }
                        }
                    }
                    else if (m is ModuleEnginesFX)
                    {
                        ModuleEnginesFX e = m as ModuleEnginesFX;
                        if (e.EngineIgnited && !e.engineShutdown)
                        {
                            foreach (Propellant v in e.propellants)
                            {
                                string propName = v.name;
                                PartResourceDefinition r = l.resourceDefinitions[propName];
                                if (propName == "IntakeAir")
                                {
                                    airDemand += v.currentRequirement;
                                    continue;
                                }
                            }
                        }
                    }
                    else if (m is ModuleResourceIntake)
                    {
                        ModuleResourceIntake i = m as ModuleResourceIntake;
                        if (i.intakeEnabled)
                        {
                            airAvailable += i.airFlow * fixedDeltaTime;
                        }
                    }
                }
            }
            data.airAvailability = airAvailable / airDemand;
            data.hasAirAvailability = true;
        }
    };



    class DataSetupWarnings : DataSource
    {
        public override void Update(Data data, Vessel vessel)
        {
            if (data.hasAerodynamics)
            {
                if (data.q < 10)
                {
                    data.warnQ = MyStyleId.Greyed;
                    data.warnStall = MyStyleId.Greyed;
                }
                else
                {
                    if (data.q > 40000)
                        data.warnQ = MyStyleId.Warn1;
                    else
                        data.warnQ = MyStyleId.Greyed;

                    if (data.stallPercentage > 0.5)
                        data.warnStall = MyStyleId.Warn2;
                    else if (data.stallPercentage > 0.005)
                        data.warnStall = MyStyleId.Warn1;
                    else
                        data.warnStall = MyStyleId.Greyed;
                }
            }
            if (data.hasAirAvailability)
            {
                if (data.airAvailability < 1.05)
                    data.warnAir = MyStyleId.Warn2;
                else if (data.airAvailability < 1.5)
                    data.warnAir = MyStyleId.Warn1;
                else
                    data.warnAir = MyStyleId.Greyed;
            }
            if (data.hasTemp)
            {
                if (data.highestRelativeTemp > 0.95)
                    data.warnTemp = MyStyleId.Warn2;
                else if (data.highestRelativeTemp > 0.8)
                    data.warnTemp = MyStyleId.Warn1;
                else
                    data.warnTemp = MyStyleId.Greyed;
            }
        }
    }



    class DataOrbitAndAltitude : DataSource
    {
        public override void Update(Data data, Vessel vessel)
        {
            Orbit o = vessel.orbit;
            CelestialBody b = vessel.mainBody;

            if (o != null && b != null)
            {
                data.periapsis = o.PeA;
                data.apoapsis = o.ApA;

                double time = Planetarium.GetUniversalTime();
                double timeToEnd = o.EndUT - time;
                double timeToAp = o.timeToAp;
                double timeToPe = o.timeToPe;

                if (data.apoapsis < data.periapsis || timeToAp <= 0)
                    timeToAp = double.PositiveInfinity; // not gona happen
                if (timeToPe <= 0)
                    timeToPe = double.PositiveInfinity;

                if (timeToEnd <= timeToPe && timeToEnd <= timeToAp && o.patchEndTransition != Orbit.PatchTransitionType.FINAL && o.patchEndTransition != Orbit.PatchTransitionType.INITIAL)
                {
                    data.timeToNode = timeToEnd;
                    if (o.patchEndTransition == Orbit.PatchTransitionType.ESCAPE) data.nextNode = Data.NextNode.Escape;
                    else if (o.patchEndTransition == Orbit.PatchTransitionType.ENCOUNTER) data.nextNode = Data.NextNode.Encounter;
                    else data.nextNode = Data.NextNode.Maneuver;
                }
                else if (timeToAp < timeToPe)
                {
                    data.timeToNode = o.timeToAp;
                    data.nextNode = Data.NextNode.Ap;
                }
                else
                {
                    data.timeToNode = timeToPe;
                    data.nextNode = Data.NextNode.Pe;
                }

                if (b.atmosphere)
                {
                    double hmin = (double)(((int)(b.maxAtmosphereAltitude * 0.33333333e-3))) * 1000;
                    data.isAtmosphericLowLevelFlight = !(data.apoapsis > hmin || data.periapsis > hmin);
                }
                else
                    data.isAtmosphericLowLevelFlight = false;
                data.isInAtmosphere = b.atmosphere && vessel.altitude < b.maxAtmosphereAltitude;
            }

            data.altitude = vessel.altitude;
            data.radarAltitude = vessel.altitude - Math.Max(0, vessel.terrainAltitude); // terrainAltitude is the deviation of the terrain from the sea level.

            data.isLanded = false;
            if (vessel.LandedOrSplashed)
            {
                double srfSpeedSqr = vessel.GetSrfVelocity().sqrMagnitude;
                if (srfSpeedSqr < 0.01)
                    data.isLanded = true;
            }
        }
    }



    class DataTemperature : DataSource
    {
        public override void Update(Data data, Vessel vessel)
        {
            data.hasTemp = data.isInAtmosphere;
            if (!data.hasTemp) return;

            data.highestTemp = double.NegativeInfinity;
            data.highestRelativeTemp = double.NegativeInfinity;
            foreach (Part p in vessel.parts)
            {
                if (p.temperature != 0f) // small gear box has p.temperature==0 - always! Bug? Who knows. Anyway i want to ignore it.
                {
                    data.highestTemp = Math.Max(p.temperature, data.highestTemp);
                    data.highestRelativeTemp = Math.Max(p.temperature / p.maxTemp, data.highestRelativeTemp);
                }
            }
        }
    }

    #endregion

    public class KFIText : MonoBehaviour
    {
        ScreenSafeGUIText ssgt1_;
        ScreenSafeGUIText ssgt2_; // child of ssgt1_
        GUIContent content_; // store this persistently to avoid reallocation on every frame!
        Vector2    size_;
        float      baseTextSize_;
        int        styleId_;
        int change_ = 0, last_change_checked_ = -1;

        public static KFIText Create(string id, int styleId)
        {
            GameObject textGO = new GameObject("KFI-" + id);
            KFIText kfi = textGO.AddComponent<KFIText>();

            kfi.ssgt1_ = textGO.AddComponent<ScreenSafeGUIText>();
            textGO.layer = 12; // navball layer

            GameObject shadowGO = new GameObject("KFI-SH-" + id);
            kfi.ssgt2_ = shadowGO.AddComponent<ScreenSafeGUIText>();
            shadowGO.layer = 12;
            shadowGO.transform.parent = textGO.transform;
            shadowGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);

            kfi.content_ = new GUIContent();

            kfi.baseTextSize_ = GuiInfo.instance.baselineFontSize;
            kfi.styleId = styleId;

            //GUIText t = o.AddComponent<GUIText>();
            //t.text = text;
            //t.alignment = TextAlignment.Left;
            //t.anchor    = anchor;
            //t.material.color = color;
            return kfi;
        }

        public static void Destroy(ref KFIText kfi)
        {
            GameObject.Destroy(kfi.gameObject);
            kfi = null;
        }

        public void OnGUI() // auto update things
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector3 global_scale = this.ssgt1_.transform.lossyScale;
            int newTextSize = Mathf.CeilToInt(baseTextSize_*global_scale.x); // have to recompute because not sure if parent scale changed
            if (newTextSize != (int)this.ssgt1_.textSize) // if scale changed, we need to update the element size
            {
                this.ssgt1_.textSize = newTextSize;
                this.ssgt2_.textSize = newTextSize;
                change_++;
            }
            if (last_change_checked_ != change_)
            {
                size_ = this.ssgt1_.textStyle.CalcSize(this.content_);
                size_ = Util.ScreenSizeToWorldSize(GuiInfo.instance.camera, size_);
                last_change_checked_ = change_;
                DMDebug.Log("Evil Update:" + this.gameObject.name);
            }
        }

        public void OnDestroy()
        {
            // release links to make it easier for the gc
            ssgt1_ = null;
            ssgt2_ = null;
            content_ = null;
        }

        public void SetStyledText(string text, int styleId)
        {
            this.text = text;
            this.styleId = styleId;
        }

        public int styleId
        {
            set
            {
                if (this.styleId_ != value)  // careful because of costly update
                {
                    this.styleId_ = value;
                    this.ssgt1_.textStyle = GuiInfo.instance.shadowed_styles[value];
                    this.ssgt2_.textStyle = GuiInfo.instance.styles[value];
                    change_++;
                }
            }
            get 
            {
                return this.styleId_;
            }
        }
        
        public string text
        {
            set
            {
                if (value != this.content_.text) // careful because of costly update
                {
                    this.content_.text = value;
                    this.ssgt1_.text = value;
                    this.ssgt2_.text = value;
                    change_++;
                }
            }
            get 
            {
                return this.content_.text;
            }
        }

        public Vector3 localPosition
        {
            set
            {
                this.ssgt1_.transform.localPosition = value;
            }
        }

        public Vector2 size
        {
            get
            {
                return size_;
            }
        }

        new public bool enabled
        {
            get
            {
                return this.ssgt1_.enabled;
            }
            set 
            {
                this.ssgt1_.enabled = value;
            }
        }
    };


    public class KFIArea : MonoBehaviour
    {
        public TextAlignment alignment;
        public List<KFIText> items;

        public static KFIArea Create(string id, Vector2 position_, TextAlignment alignment_, Transform parent) // factory, creates game object with attached FKIArea
        {
            GameObject go = new GameObject("KFI-AREA-"+id);
            KFIArea kfi = go.AddComponent<KFIArea>();
            kfi.alignment = alignment_;
            kfi.items = new List<KFIText>();
            go.transform.parent = parent;
            go.transform.localPosition = position_;
            return kfi;
        }

        public static void Destroy(KFIArea a) // destroy its gameobject
        {
            GameObject.Destroy(a.gameObject);
        }

        public void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 size = Vector2.zero;
            foreach (KFIText t in items)
            {
                if (!t.enabled) continue;
                Vector2 s = t.size; // it is the size in screen space unfortunately
                size = new Vector2(Mathf.Max(size.x, s.x), size.y + s.y);
            }
            Vector2 p = Vector2.zero;
            if (alignment == TextAlignment.Right) p.x -= size.x;
            p.y += size.y;
            foreach (KFIText t in items)
            {
                if (!t.enabled) continue;
                t.localPosition = p;
                p.y += t.size.y;
            }
        }

        public void OnDestroy()
        {
            items.Clear();
        }

        public void Add(KFIText t)
        {
            t.gameObject.transform.parent = this.gameObject.transform;
            items.Add(t);
        }
    };



    public class GuiInfo
    {
        static GuiInfo instance_;

        NavBallBurnVector burnVector_ = null;

        public GameObject navballGameObject = null;
        public Camera     camera = null;

        float navBallLeftBoundary_;
        float navBallRightBoundary_;
        float navBallRightBoundaryWithGauge_;

        public GUIStyle prototypeStyle;

        public float anchorScale;
        public float uiScalingFactor = 1f;
        public int baselineFontSize = 14; // font size @ "normal" UI scale setting
        public int fontSize;

        public GUIStyle[] styles = { null, null, null, null, null };
        public GUIStyle[] shadowed_styles = { null, null, null, null, null };

        public GuiInfo()
        {
            Init();
        }

        public void Init()
        {
            GameObject go = GameObject.Find("speedText");
            prototypeStyle = go.GetComponent<ScreenSafeGUIText>().textStyle;

            // GUI functions must only be called in OnGUI ...
            var s = new GUIStyle(prototypeStyle);
            s.SetColor(Color.white);
            s.padding = new RectOffset(0, 0, 0, 1);
            s.margin = new RectOffset(0, 0, 0, 0);
            s.fontSize = Mathf.RoundToInt(baselineFontSize * uiScalingFactor);
            s.wordWrap = false;
            styles[MyStyleId.Plain] = s;
            styles[MyStyleId.Emph] = new GUIStyle(s);
            styles[MyStyleId.Emph].fontStyle = FontStyle.Bold;
            s = new GUIStyle(s);
            s.SetColor(Color.grey);
            styles[MyStyleId.Greyed] = s;
            s = new GUIStyle(s);
            s.SetColor(Color.yellow);
            styles[MyStyleId.Warn1] = s;
            s = new GUIStyle(s);
            s.SetColor(Color.red);
            styles[MyStyleId.Warn2] = s;
            styles[MyStyleId.Warn1].fontStyle = styles[MyStyleId.Warn2].fontStyle = FontStyle.Bold;

            for (int i = 0; i < styles.Length; ++i)
            {
                s = new GUIStyle(styles[i]);
                s.SetColor(Color.black);
                s.contentOffset = new Vector2(1f, 2f);
                shadowed_styles[i] = s;
            }

            Update();
        }

        public void Update()
        {
            navballGameObject = GameObject.Find("NavBall");
            GameObject maneuverVectorGameObject = GameObject.Find("maneuverVector");
            burnVector_ = maneuverVectorGameObject.GetComponent<NavBallBurnVector>();
            Camera cam = ScreenSafeUI.referenceCam;

            float navballWidth = 0.07f;
            float navballWidthWithGauge = 0.12f;

            Vector3 p = cam.WorldToScreenPoint(navballGameObject.transform.position);
            Vector3 p2 = cam.WorldToScreenPoint(navballGameObject.transform.localToWorldMatrix.MultiplyPoint(new Vector3(navballWidth, 0, 0)));
            Vector3 p3 = cam.WorldToScreenPoint(navballGameObject.transform.localToWorldMatrix.MultiplyPoint(new Vector3(navballWidthWithGauge, 0, 0)));
            navBallRightBoundary_ = p2.x;
            navBallLeftBoundary_ = p.x - (p2.x - p.x);
            navBallRightBoundaryWithGauge_ = p3.x;

            ScreenSafeUI.fetch.centerAnchor.bottom.hasChanged = false; // this is probably not a good idea. It will break things that also use the hasChanged flag ...

            anchorScale = ScreenSafeUI.fetch.centerAnchor.bottom.localScale.x;
            // how much the fonts and everything must be scaled relative to a
            // reference GUI size (the normal KSP setting, i believe).
            uiScalingFactor = 0.6f / ScreenSafeUI.referenceCam.orthographicSize; // 1.175 = orthographicSize for "normal" UI scale setting
            uiScalingFactor *= anchorScale; // scale font with navball

            fontSize = Mathf.RoundToInt(baselineFontSize * uiScalingFactor);

            camera = ScreenSafeUI.fetch.uiCameras.FirstOrDefault(c => (c.cullingMask & (1<<12)) != 0);
        }

        public bool hasChanged
        {
            get { return ScreenSafeUI.fetch.centerAnchor.bottom.hasChanged; }
        }

        public bool showGauge
        {
            get { return burnVector_.deltaVGauge != null && burnVector_.deltaVGauge.gameObject.activeSelf == true; }
        }

        public float screenAnchorLeft
        {
            get { return navBallLeftBoundary_; }
        }

        public float screenAnchorRight
        {
            get { return showGauge ? navBallRightBoundaryWithGauge_ : navBallRightBoundary_; }
        }

        public static GuiInfo instance
        {
            get
            {
                if (instance_ == null)
                    instance_ = new GuiInfo();
                return instance_;
            }
        }
    }



    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class DMFlightData : MonoBehaviour
    {
        const double updateIntervall = 0.1;
        double dtSinceLastUpdate = 0;

        bool displayUI = true;
        bool displayUIByGuiEvent = true;

        Data data;
        List<DataSource> dataSources = new List<DataSource>();

        int timeSecondsPerDay = 0;
        int timeSecondsPerYear = 0;

        static Toolbar.IButton toolbarButton;

        #region Config & Data Acquisition

        void Awake()
        {
            dataSources.Clear();
            //DMDebug.Log("DMFlightData Awake");
            bool hasFar = false;
            foreach (var assembly in AssemblyLoader.loadedAssemblies)
            {
                //DMDebug.Log(assembly.name);
                if (assembly.name == "FerramAerospaceResearch")
                {
                    var types = assembly.assembly.GetExportedTypes();
                    foreach (Type t in types)
                    {
                        //DMDebug.Log(t.FullName);
                        if (t.FullName.Equals("ferram4.FARControlSys"))
                        {
                            dataSources.Add(new DataFAR(t));
                            hasFar = true;
                        }
                    }
                }
                if (assembly.name == "DeadlyReentry")
                {
                    dataSources.Add(new DataTemperature());
                }
            }
            if (hasFar == false)
                dataSources.Add(new DataIntakeAirStock());
            dataSources.Add(new DataOrbitAndAltitude());
            dataSources.Add(new DataSetupWarnings());

            data = new Data();

            if (GameSettings.KERBIN_TIME)
            {
                CelestialBody b = FlightGlobals.Bodies.Find((b_) => b_.name == "Kerbin");
                timeSecondsPerDay = (int)b.rotationPeriod;
                timeSecondsPerYear = (int)b.orbit.period;
                // when this fails an exception should be visible in the debug log and 
                // the time to the next node display should show NaNs and Infs so it should
                // be pretty clear when something goes wrong at this place
            }
            else
            {
                timeSecondsPerDay = 24 * 3600;
                timeSecondsPerYear = timeSecondsPerDay * 365;
            }

            toolbarButton = Toolbar.ToolbarManager.Instance.add("KerbalFlightData", "damichelsflightdata");
            toolbarButton.TexturePath = "KerbalFlightData/toolbarbutton";
            toolbarButton.ToolTip = "KerbalFlightData On/Off Switch";
            toolbarButton.Visibility = new Toolbar.GameScenesVisibility(GameScenes.FLIGHT);
            toolbarButton.Enabled = true;
            toolbarButton.OnClick += (e) =>
            {
                enabled = !enabled;
            };

            GameEvents.onHideUI.Add(() =>
            {
                displayUIByGuiEvent = false;
            });

            GameEvents.onShowUI.Add(() =>
            {
                displayUIByGuiEvent = true;
            });
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

            dtSinceLastUpdate = Double.PositiveInfinity;
        }


        public void OnDestroy()
        {
            SaveSettings();
            CleanupGUI();
        }


        void LateUpdate()
        {
#if !DEBUG
        if (dtSinceLastUpdate > updateIntervall) // update every so and so fraction of a second
        {
            dtSinceLastUpdate = 0;
        }
        else
        {
            dtSinceLastUpdate += Time.deltaTime;
            return;
        }
#endif

            displayUI = false; // don't show anything unless some stuff is all right
            if (!FlightGlobals.ready) return;

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return;

            if (vessel.isEVA || vessel.state == Vessel.State.DEAD)
            {
                return;
            }
            else displayUI = true; // at this point something should probably be shown

            foreach (DataSource d in dataSources)
            {
                d.Update(data, vessel);
            }

#if DEBUG
            if (Input.GetKeyDown(KeyCode.I))
            {
                guiInfo = new GuiInfo();
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
                DMDebug dbg = new DMDebug();
                //dbg.PrintHierarchy(ScreenSafeUI.fetch);
                //dbg.PrintHierarchy(GameObject.Find("collapseExpandButton"));
                //dbg.PrintHierarchy(ScreenSafeUI.fetch.centerAnchor.bottom);
                dbg.PrintHierarchy(GameObject.Find("speedText"));
                dbg.Out("---------------------------------------------------------", 0);
                dbg.PrintHierarchy(GameObject.Find("KFI-test1"));
                dbg.Out("---------------------------------------------------------", 0);
                dbg.PrintHierarchy(GameObject.Find("UI camera"));
                dbg.Out("---------------------------------------------------------", 0);
                //dbg.PrintHierarchy(GameObject.Find("maneuverVector"));
                //int indent;
                //dbg.PrintGameObjectHierarchUp(ScreenSafeUI.fetch.centerAnchor.bottom.gameObject, out indent);
                //dbg.PrintGameObjectHierarchy(ScreenSafeUI.fetch.centerAnchor.bottom.gameObject, indent);
                dbg.PrintGameObjectHierarchy(ScreenSafeUI.fetch.gameObject, 0);
                var f = KSP.IO.TextWriter.CreateForType<DMFlightData>("DMdebugoutput.txt", null);
                f.Write(dbg.ToString());
                f.Close();
            }
#endif
        }
        #endregion

        #region GUIUtil
        protected static String FormatPressure(double x)
        {
            if (x > 1.0e6)
            {
                x *= 1.0e-6;
                return x.ToString(x < 10 ? "F1" : "F0") + " MPa";
            }
            else
            {
                x *= 1.0e-3;
                return x.ToString(x < 10 ? "F1" : "F0") + " kPa";
            }
        }

        protected static String FormatAltitude(double x)
        {
            double a = Math.Abs(x);
            if (a >= 1.0e9)
            {
                x *= 1.0e-9;
                a *= 1.0e-9;
                return x.ToString(a < 10 ? "F2" : (a < 100 ? "F1" : "F0")) + " Gm";
            }
            if (a >= 1.0e6)
            {
                x *= 1.0e-6;
                a *= 1.0e-6;
                return x.ToString(a < 10 ? "F2" : (a < 100 ? "F1" : "F0")) + " Mm";
            }
            else
            {
                x *= 1.0e-3;
                a *= 1.0e-3;
                return x.ToString(a < 10 ? "F2" : (a < 100 ? "F1" : "F0")) + " km";
            }
        }


        protected static String FormatRadarAltitude(double x)
        {
            double a = Math.Abs(x);
            if (a >= 1.0e3)
            {
                x *= 1.0e-3;
                a *= 1.0e-3;
                return x.ToString(a < 10 ? "F2" : (a < 100 ? "F1" : "F0")) + " km";
            }
            else
            {
                return x.ToString("F0") + " m";
            }
        }


        protected String FormatTime(double x_)
        {
            const int MIN = 60;
            const int H = 3600;
            int D = timeSecondsPerDay;
            int Y = timeSecondsPerYear;

            int x = (int)x_;
            int y, d, m, h, s;
            y = x / Y;
            x = x % Y;
            d = x / D;
            x = x % D;
            h = x / H;
            x = x % H;
            m = x / MIN;
            x = x % MIN;
            s = x;
            int size = 3;
            string[] arr = new string[size];
            int idx = 0;
            if (y > 0)
                arr[idx++] = y.ToString() + "y";
            if (d > 0 || idx > 0)
                arr[idx++] = d.ToString() + "d";
            if ((h > 0 || idx > 0) && idx < size)
                arr[idx++] = h.ToString() + "h";
            if ((m > 0 || idx > 0) && idx < size)
                arr[idx++] = m.ToString() + "m";
            if ((s > 0 || idx > 0) && idx < size)
                arr[idx++] = s.ToString() + "s";
            return string.Join(" ", arr, 0, idx);
        }
        #endregion

        #region GUI

        const float windowBottomOffset = 5f;

        GuiInfo guiInfo;
        KFIText kfiMach, kfiAlt, kfiAir, kfiStall, kfiQ, kfiTemp;
        KFIArea leftArea, rightArea;


        void SetupGUI()
        {
            guiInfo = GuiInfo.instance;
            guiInfo.Init();

            leftArea = KFIArea.Create("left", new Vector2(0.1f, 0f), TextAlignment.Right, guiInfo.navballGameObject.transform);
            rightArea = KFIArea.Create("right", new Vector2(-0.1f, 0f), TextAlignment.Left, guiInfo.navballGameObject.transform);

            kfiAlt = KFIText.Create("alt", MyStyleId.Emph);
            kfiMach = KFIText.Create("mach", MyStyleId.Emph);
            kfiAir = KFIText.Create("air", MyStyleId.Greyed);
            kfiStall = KFIText.Create("stall", MyStyleId.Greyed);
            kfiQ = KFIText.Create("q", MyStyleId.Greyed);
            kfiTemp = KFIText.Create("temp", MyStyleId.Greyed);

            leftArea.Add(kfiAlt);
            rightArea.Add(kfiMach);
            rightArea.Add(kfiAir);
            rightArea.Add(kfiStall);
            rightArea.Add(kfiQ);
            rightArea.Add(kfiTemp);

            //DMDebug.Log("kfimach = " + kfiMach == null ? "null" : kfiMach.ToString());
            /* //not going to happen ... 
             * // i'm trying to set up a Font instance with a custom bitmap font, but unluckly this draws absolutely nothing on screen, no error as well.
            theFont = new Font("DMFont");
            byte[] fubar = ((Texture2D)GUI.skin.font.material.mainTexture).EncodeToPNG();
            KSP.IO.File.WriteAllBytes<DMFlightData>(fubar, "shit.png"); // as if this was going to work ... it doesn't ...
            // next try
            Texture2D fontTex = GameDatabase.Instance.GetTexture("KerbalFlightData/Textures/plain_white", false);
            fontTex.filterMode = FilterMode.Bilinear;
            DMDebug.Log(fontTex == null ? "fontTex = null" : fontTex.ToString());
            DMDebug.Log(GUI.skin.font.material == null ? "s.font.material = null" : GUI.skin.font.material.ToString());
            theFont.characterInfo = new CharacterInfo[1];
            theFont.characterInfo[0].index = 65; // this is the decimal code for an 'A'
            theFont.characterInfo[0].uv = new Rect(0.527f, 0.156f, 0.054f, 0.125f);
            theFont.characterInfo[0].width = 8f;
            theFont.characterInfo[0].vert = new Rect(0f, -2f, 10f, -15f);
            theFont.characterInfo[0].size = 12;
            theFont.characterInfo[0].style = FontStyle.Normal;
            theFont.material = new Material(GUI.skin.font.material);
            theFont.material.mainTexture = fontTex; */

        }

        void CleanupGUI()
        {
            guiInfo = null;
            KFIArea.Destroy(leftArea);
            KFIArea.Destroy(rightArea);
            kfiMach = kfiAlt = kfiAir = kfiStall = kfiQ = kfiTemp = null;
            //KFIText.Destroy(kfiMach);
            //KFIText.Destroy(kfiAlt);
            //leftArea = null;
            //rightArea = null;
        }


        protected void OnGUI()
        {
            // configures the layout here
            if (Event.current.type != EventType.Repaint) return; // one of these events is sent per frame. All other events should be ignored.
            DMDebug.Log("Repaint Event");

            if (!displayUI || !displayUIByGuiEvent) return;
            {

                if (!FlightUIModeController.Instance.navBall.expanded || !FlightUIModeController.Instance.navBall.enabled) return;

                switch (CameraManager.Instance.currentCameraMode)
                {
                    case CameraManager.CameraMode.IVA:
                        return;
                }
            }

            if (guiInfo == null)
                SetupGUI();
            if (guiInfo.hasChanged)
                guiInfo.Update();
            //DMDebug.Log("guiInfo = " + (guiInfo == null ? "null" : guiInfo.ToString()));

            if (data.isInAtmosphere && !data.isLanded)
            {
                if (data.hasAerodynamics)
                {
                    kfiMach.text = "Mach " + data.machNumber.ToString("F2");
                }
                if (data.hasAirAvailability)
                {
                    String intakeLabel = "Intake";
                    if (data.airAvailability < 2d) intakeLabel += "  " + (data.airAvailability * 100d).ToString("F0") + "%";
                    kfiAir.SetStyledText(intakeLabel, data.warnAir);
                }
                if (data.hasAerodynamics)
                {
                    kfiQ.SetStyledText("Q  " + FormatPressure(data.q), data.warnQ);
                    kfiStall.SetStyledText("Stall", data.warnStall);
                }
                if (data.hasTemp)
                {
                    kfiTemp.SetStyledText("T " + data.highestTemp.ToString("F0") + " °C", data.warnTemp);
                }
            }

            if (data.radarAltitude < 5000)
            {
                kfiAlt.SetStyledText("Alt " + FormatRadarAltitude(data.radarAltitude) + " R", data.radarAltitude < 200 ? MyStyleId.Warn1 : MyStyleId.Emph);
            }
            else
            {
                kfiAlt.SetStyledText("Alt " + FormatAltitude(data.altitude), MyStyleId.Emph);
            }
            //if (data.isAtmosphericLowLevelFlight == false)
            //{
            //    String timeLabel = "";
            //    switch (data.nextNode)
            //    {
            //        case Data.NextNode.Ap: timeLabel = "Ap"; break;
            //        case Data.NextNode.Pe: timeLabel = "Pe"; break;
            //        case Data.NextNode.Encounter: timeLabel = "En"; break;
            //        case Data.NextNode.Maneuver: timeLabel = "Man"; break;
            //        case Data.NextNode.Escape: timeLabel = "Esc"; break;
            //    }
            //    timeLabel = "T" + timeLabel + " -";
            //    PaintLabel(timeLabel + FormatTime(data.timeToNode), MyStyleId.Plain);
            //    if (data.nextNode == Data.NextNode.Ap || data.nextNode == Data.NextNode.Pe)
            //    {
            //        PaintLabel("Ap " + FormatAltitude(data.apoapsis), data.nextNode == Data.NextNode.Ap ? MyStyleId.Emph : MyStyleId.Plain);
            //        PaintLabel("Pe " + FormatAltitude(data.periapsis), data.nextNode == Data.NextNode.Pe ? MyStyleId.Emph : MyStyleId.Plain);
            //    }
            //}
        }

        #endregion
    }


}