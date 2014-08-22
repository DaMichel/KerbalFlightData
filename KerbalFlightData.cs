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
    #region utilities

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
        [System.Diagnostics.Conditional("DEBUG")] // this makes it execute only in debug builds, including argument evaluations. It is very efficient. the compiler will just skip those calls.
        public static void Log2(string s)
        {
            DMDebug.Log(s);
        }

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

    #endregion

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

    #region GUI classes

    public static class MyStyleId
    {
        public const int Plain = 0;
        public const int Greyed = 1;
        public const int Warn1 = 2;
        public const int Warn2 = 3;
        public const int Emph = 4;
    };



    /* this scripts manages a piece of text. It allocates the main text plus some clones for shadowing. 
     * The clones get a dark color and are drawn behind the main text with a slight offset */
    public class KFIText : MonoBehaviour
    {
        // GUITexts are positioned in viewport space [0,1]^2
        GUIText    gt1_; 
        GUIText    gt2_; // child of gt1_
        GUIText    gt3_; // child of gt1_
        int        styleId_ = -1;
        Rect       screenRect_;
        int change_ = 0, last_change_checked_ = -1; // with this i check if the screen space rect must be recomputed

        public static KFIText Create(string id, int styleId)
        {
            GameObject textGO = new GameObject("KFI-" + id);
            textGO.layer = 12; // navball layer

            KFIText kfi = textGO.AddComponent<KFIText>();
            
            kfi.gt1_ = textGO.AddComponent<GUIText>();
            kfi.gt1_.anchor = TextAnchor.LowerLeft;
            kfi.gt1_.alignment = TextAlignment.Left;

            GameObject shadowGO = new GameObject("KFI-SH-" + id);            
            shadowGO.layer = 12;
            shadowGO.transform.parent = textGO.transform;
            shadowGO.transform.localPosition = Vector3.zero;

            kfi.gt2_ = shadowGO.AddComponent<GUIText>();
            kfi.gt2_.anchor = TextAnchor.LowerLeft;
            kfi.gt2_.alignment = TextAlignment.Left;

            shadowGO = new GameObject("KFI-SH2-" + id);
            shadowGO.layer = 12;
            shadowGO.transform.parent = textGO.transform;
            shadowGO.transform.localPosition = Vector3.zero;

            kfi.gt3_ = shadowGO.AddComponent<GUIText>();
            kfi.gt3_.anchor = TextAnchor.LowerLeft;
            kfi.gt3_.alignment = TextAlignment.Left;

            kfi.styleId = styleId;
            return kfi;
        }
        
        public void OnDestroy()
        {
            DMDebug.Log2(this.name + " OnDestroy");
            // release links to make it easier for the gc
            gt1_ = null;
            gt2_ = null;
            gt3_ = null;
        }

        public int styleId
        {
            set
            {
                if (this.styleId_ != value)  // careful because of potentially costly update
                {
                    this.styleId_ = value;
                    GUIStyle s = GuiInfo.instance.styles[value];
                    this.gt1_.fontStyle = s.fontStyle;
                    this.gt1_.fontSize  = GuiInfo.instance.fontSize;
                    this.gt1_.font      = s.font;
                    this.gt1_.material.color = s.active.textColor;

                    this.gt2_.fontStyle = s.fontStyle;
                    this.gt2_.fontSize = GuiInfo.instance.fontSize;
                    this.gt2_.font = s.font;
                    this.gt2_.material.color = XKCDColors.DarkGrey;
                    this.gt2_.pixelOffset = new Vector2(0f, -1f);

                    this.gt3_.fontStyle = s.fontStyle;
                    this.gt3_.fontSize = GuiInfo.instance.fontSize;
                    this.gt3_.font = s.font;
                    this.gt3_.material.color = XKCDColors.DarkGrey;
                    this.gt3_.pixelOffset = new Vector2(1f, -1f);
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
                if (value != this.gt1_.text) // careful because of costly update
                {
                    this.gt1_.text = value;
                    this.gt2_.text = value;
                    this.gt3_.text = value;
                    ++change_;
                }
            }
            get 
            {
                return this.gt1_.text;
            }
        }

        public int fontSize
        {
            set
            {
                this.gt1_.fontSize = GuiInfo.instance.fontSize;
                this.gt2_.fontSize = GuiInfo.instance.fontSize;
                this.gt3_.fontSize = GuiInfo.instance.fontSize;
                ++change_;
            }
            get 
            {
                return this.gt1_.fontSize;
            }
        }

        public Vector3 localPosition
        {
            set
            {
                this.gameObject.transform.localPosition = value;
            }
        }

        public Rect screenRect // in viewport space
        {
            get
            {
                if (change_ != last_change_checked_)
                {
                    Rect r = this.gt1_.GetScreenRect(); // this gives us the coordinates in screen space, based on pixels
                    Camera c = GuiInfo.instance.camera;
                    Vector2 p0 = c.ScreenToViewportPoint(r.min); 
                    Vector2 p1 = c.ScreenToViewportPoint(r.max);
                    screenRect_ = new Rect(p0.x, p1.y, p1.x - p0.x, p1.y - p0.y);
                    last_change_checked_ = change_;
                }
                return screenRect_;
            }
        }

        public bool enableGameObject
        {
            set 
            { 
                if (this.gameObject.activeSelf != value) 
                {
                    DMDebug.Log2(this.name + " enabled=" + value);
                    this.gameObject.SetActive(value);
                }
            }
            get { return this.gameObject.activeSelf; }
        }
    };




    /* This class represents the left/right text areas. Texts are managed as children (by Unity GameObjects).
     * The point is to have the area auto-expand the contain one text per row in the vertical and 
     * to auto-expand in width and to align the text */
    public class KFIArea : MonoBehaviour
    {
        public TextAlignment alignment;
        public List<KFIText> items;
        float maximalWidth = 0f;

        /* note: alignment is actually the alignment of the area. e.g. right-alignment means that the anchor point
         * is at the right bottom(!) corner. The text inside is always left-aligned*/
        public static KFIArea Create(string id, Vector2 position_, TextAlignment alignment_, Transform parent) // factory, creates game object with attached FKIArea
        {
            GameObject go = new GameObject("KFI-AREA-"+id);
            KFIArea kfi = go.AddComponent<KFIArea>();
            kfi.alignment = alignment_;
            kfi.useGUILayout = false;
            kfi.items = new List<KFIText>();
            go.transform.parent = parent;
            go.transform.localPosition = position_;
            return kfi;
        }

        public void UpdateLayout() // called from the KerbalFlightData script
        {
            //DMDebug.Log2(this.name + " LateUpdate");
            float w = maximalWidth, h = 0;
            foreach (KFIText t in items)
            {
                if (!t || !t.enableGameObject) continue;
                Rect r = t.screenRect;
                //DMDebug.Log2("r("+t.name+") = " + r.ToString());
                w = Mathf.Max(w, r.width);
                h += r.height;
            }
            Vector2 p = Vector2.zero;
            if (alignment == TextAlignment.Right) p.x -= w;
            p.y += h;
            foreach (KFIText t in items)
            {
                if (!t || !t.enableGameObject) continue;
                t.localPosition = p;
                p.y -= t.screenRect.height;
            }
            maximalWidth = w;
        }

        public void ResetLayout() // when ui size changes
        {
            maximalWidth = 0;
        }

        void OnDestroy()
        {
            DMDebug.Log2(this.name + " OnDestroy");
            items.Clear();
        }

        public void Add(KFIText t)
        {
            t.gameObject.transform.parent = this.gameObject.transform;
            t.localPosition = Vector3.zero;
            items.Add(t);
        }

        public bool enableGameObject
        {
            set 
            { 
                if (this.gameObject.activeSelf != value) 
                {
                    DMDebug.Log2(this.name + " enabled=" + value);
                    this.gameObject.SetActive(value); 
                }
            }
            get { return this.gameObject.activeSelf; }
        }
    };




    /* this class contains information for styling and positioning the texts */
    public class GuiInfo
    {
        static GuiInfo instance_ = new GuiInfo(); // allocate when the code loads

        NavBallBurnVector burnVector_ = null;
        GameObject navballGameObject = null;
        float uiScalingFactor;

        const float navballWidth = 0.07f;
        const float navballGaugeWidth = 0.030f;
        const float navballGaugeWidthNonscaling = 0.030f;
        const int baselineFontSize = 16; // font size @ "normal" UI scale setting

        public Camera camera = null;
        public int   fontSize;
        public float screenAnchorRight;
        public float screenAnchorLeft;
        public float screenAnchorBottom;
        public bool  ready = false;

        public GUIStyle prototypeStyle;
        public GUIStyle[] styles = { null, null, null, null, null };

        public void Init()
        {
            GameObject go = GameObject.Find("speedText");
            prototypeStyle = go.GetComponent<ScreenSafeGUIText>().textStyle;

            // GUI functions must only be called in OnGUI ... but ... you can apparently still clone styles ...
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
            s.SetColor(XKCDColors.Grey);
            styles[MyStyleId.Greyed] = s;
            s = new GUIStyle(s);
            s.SetColor(Color.yellow);
            styles[MyStyleId.Warn1] = s;
            s = new GUIStyle(s);
            s.SetColor(Color.red);
            styles[MyStyleId.Warn2] = s;
            styles[MyStyleId.Warn1].fontStyle = styles[MyStyleId.Warn2].fontStyle = FontStyle.Bold;

            navballGameObject = GameObject.Find("MapCollapse_navball");
            GameObject maneuverVectorGameObject = GameObject.Find("maneuverVector");
            burnVector_ = maneuverVectorGameObject.GetComponent<NavBallBurnVector>();
            camera = ScreenSafeUI.referenceCam;

            Update();

            ready = true;
        }

        public void Destroy()
        {
            // It's safer to remove all references and not have "dead" objects around. Even more so because the instance is always accessible .
            burnVector_ = null;
            navballGameObject = null;
            camera = null;
            prototypeStyle = null;
            for (int i=0; i<styles.Length; ++i) styles[i] = null;
            ready = false;
        }

        public void Update()
        {
            // how much the fonts and everything must be scaled relative to a
            // reference GUI size - the normal KSP setting.
            float anchorScale = ScreenSafeUI.fetch.centerAnchor.bottom.localScale.x;
            uiScalingFactor = 0.6f / camera.orthographicSize; // 0.6 = orthographicSize for "normal" UI scale setting
            uiScalingFactor *= anchorScale; // scale font with navball

            // update the anchor positions and the required font size
            Vector3 p = camera.WorldToViewportPoint(navballGameObject.transform.position);
            bool hasGauge = burnVector_.deltaVGauge != null && burnVector_.deltaVGauge.gameObject.activeSelf == true;
            if (hasGauge)
                screenAnchorRight = p.x + navballWidth * uiScalingFactor + navballGaugeWidth * uiScalingFactor + navballGaugeWidthNonscaling;
            else
                screenAnchorRight = p.x + navballWidth * uiScalingFactor;            
            screenAnchorLeft   = p.x - navballWidth * uiScalingFactor;
            screenAnchorBottom = p.y;

            fontSize = Mathf.RoundToInt(baselineFontSize * uiScalingFactor);
        }

        public static GuiInfo instance
        {
            get { return instance_; }
        }
    }


    #endregion


    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class DMFlightData : MonoBehaviour
    {
        double dtSinceLastUpdate;

        bool displayUIByGuiEvent = true;
        bool displayUIByToolbarClick = true;

        Data data;
        List<DataSource> dataSources = new List<DataSource>();

        int timeSecondsPerDay;
        int timeSecondsPerYear;

        static Toolbar.IButton toolbarButton;

        // gui stuff
        KFIText[] texts = null;
        int[] markers = null;
        int markerMaster = 0;
        KFIArea leftArea, rightArea;
        static class TxtIdx
        {
            public const int MACH = 0, ALT = 1, AIR = 2, STALL = 3, Q = 4, TEMP = 5, TNODE = 6, AP = 7, PE = 8;
        };

        void Awake() // Awake is called when the script instance is being loaded.
        {
            DMDebug.Log2(name + " Awake!");
            dataSources.Clear();
            bool hasFar = false;
            foreach (var assembly in AssemblyLoader.loadedAssemblies)
            {
                //DMDebug.Log2(assembly.name);
                if (assembly.name == "FerramAerospaceResearch")
                {
                    var types = assembly.assembly.GetExportedTypes();
                    foreach (Type t in types)
                    {
                        //DMDebug.Log2(t.FullName);
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
                displayUIByToolbarClick = !displayUIByToolbarClick;
                enabled = displayUIByToolbarClick && displayUIByGuiEvent;
            };

            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);

            LoadSettings();
        }


        void OnHideUI()
        {
            displayUIByGuiEvent = false;
            enabled = displayUIByToolbarClick && displayUIByGuiEvent;
        }


        void OnShowUI()
        {
            displayUIByGuiEvent = true;
            enabled = displayUIByToolbarClick && displayUIByGuiEvent;
        }


        void SaveSettings()
        {
            ConfigNode settings = new ConfigNode();
            settings.name = "SETTINGS";
            settings.AddValue("active", displayUIByToolbarClick);
            settings.Save(AssemblyLoader.loadedAssemblies.GetPathByType(typeof(DMFlightData)) + "/settings.cfg");
        }


        void LoadSettings()
        {
            ConfigNode settings = new ConfigNode();
            settings = ConfigNode.Load(AssemblyLoader.loadedAssemblies.GetPathByType(typeof(DMFlightData)) + @"\settings.cfg".Replace('/', '\\'));

            if (settings != null)
            {
                if (settings.HasValue("active")) displayUIByToolbarClick = bool.Parse(settings.GetValue("active"));
            }
        }


        void Start() //Start is called on the frame when a script is enabled just before any of the Update methods is called the first time.
        {
            DMDebug.Log2(name + " start!");
        }


        void OnEnable() //	This function is called when the object becomes enabled and active.
        {
            DMDebug.Log2(name + " enabled!");
            dtSinceLastUpdate = Double.PositiveInfinity; // full update next time
        }


        void OnDisable() //	This function is called when the behaviour becomes disabled () or inactive.
        {
            DMDebug.Log2(name + " disabled!");
            ToggleDisplay(false); // update won't be called so lets disable the display here
        }


        void OnDestroy() // This function is called when the MonoBehaviour will be destroyed.
        {
            DMDebug.Log2(name + " destroyed!");
            SaveSettings();

            GuiInfo.instance.Destroy();
            // fun fact: all my MonoBehaviour derivatives are destroyed automagically
            texts = null; // texts are actually destroyed by their parents
            markers = null;
            if (leftArea) GameObject.Destroy(leftArea.gameObject); // better be safe.
            if (rightArea) GameObject.Destroy(rightArea.gameObject);
            leftArea = null;
            rightArea = null;

            // unregister, or else errors occur
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
        }


        bool CheckShouldDisplayBeOn()
        {
            if (!FlightGlobals.ready) return false;

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return false;

            if (vessel.isEVA || vessel.state == Vessel.State.DEAD)
            {
                return false;
            }
            
            if (/*!FlightUIModeController.Instance.navBall.expanded ||*/ !FlightUIModeController.Instance.navBall.enabled) return false;

            switch (CameraManager.Instance.currentCameraMode)
            {
                case CameraManager.CameraMode.IVA:
                    return false;
            }

            return true;
        }


        bool CheckFullUpdateTimer()
        {
            if (dtSinceLastUpdate > 0.25) // update every so and so fraction of a second
            {
                dtSinceLastUpdate = 0;
                return true;
            }
            else return false;
        }


        void ToggleDisplay(bool on)
        {   
            // need to check if the areas are still there because on destruction of the scene they might be already gone without knowing
            if (leftArea) leftArea.enableGameObject = on;
            if (rightArea) rightArea.enableGameObject = on;                
        }

        // set text strings, style and mark as alive
        void SetAndMark(int id, string txt, int styleId) 
        {
            markers[id] = markerMaster;
            texts[id].text = txt;
            texts[id].styleId = styleId;
        }


        void LateUpdate()
        {
            dtSinceLastUpdate += Time.deltaTime;

            if (!GuiInfo.instance.ready)
                SetupGUI();
            
            bool on = CheckShouldDisplayBeOn();
            ToggleDisplay(on);

            if (on) // update text fonts and text area positions
            {
                GuiInfo guiInfo = GuiInfo.instance;
                int lastFontSize = guiInfo.fontSize;
                guiInfo.Update();
                if (lastFontSize != guiInfo.fontSize)
                {
                    foreach (var t in texts)
                        t.fontSize = guiInfo.fontSize;
                    leftArea.ResetLayout();
                    rightArea.ResetLayout();
                    leftArea.UpdateLayout();
                    rightArea.UpdateLayout();
                }

                // attach areas to the navball basically
                leftArea.transform.localPosition = new Vector2(guiInfo.screenAnchorLeft, guiInfo.screenAnchorBottom);
                rightArea.transform.localPosition = new Vector2(guiInfo.screenAnchorRight, guiInfo.screenAnchorBottom);
            }

            if (on && CheckFullUpdateTimer())
            {
                foreach (DataSource d in dataSources)
                {
                    d.Update(data, FlightGlobals.ActiveVessel);
                }

                ++markerMaster;
                if (data.isInAtmosphere && !data.isLanded)
                {
                    if (data.hasAerodynamics)
                    {
                        SetAndMark(TxtIdx.MACH, "Mach " + data.machNumber.ToString("F2"), MyStyleId.Emph);
                    }
                    if (data.hasAirAvailability)
                    {
                        String intakeLabel = "Intake";
                        if (data.airAvailability < 2d) intakeLabel += "  " + (data.airAvailability * 100d).ToString("F0") + "%";
                        SetAndMark(TxtIdx.AIR, intakeLabel, data.warnAir);
                    }
                    if (data.hasAerodynamics)
                    {
                        SetAndMark(TxtIdx.Q, "Q  " + FormatPressure(data.q), data.warnQ);
                        SetAndMark(TxtIdx.STALL, "Stall", data.warnStall);
                    }
                    if (data.hasTemp)
                    {
                        SetAndMark(TxtIdx.TEMP, "T " + data.highestTemp.ToString("F0") + " °C", data.warnTemp);
                    }
                }

                if (data.radarAltitude < 5000)
                {
                    SetAndMark(TxtIdx.ALT, "Alt " + FormatRadarAltitude(data.radarAltitude) + " R", data.radarAltitude < 200 ? MyStyleId.Warn1 : MyStyleId.Emph);
                }
                else
                {
                    SetAndMark(TxtIdx.ALT, "Alt " + FormatAltitude(data.altitude), MyStyleId.Emph);
                }

                if (data.isAtmosphericLowLevelFlight == false)
                {
                    String timeLabel = "";
                    switch (data.nextNode)
                    {
                        case Data.NextNode.Ap: timeLabel = "Ap"; break;
                        case Data.NextNode.Pe: timeLabel = "Pe"; break;
                        case Data.NextNode.Encounter: timeLabel = "En"; break;
                        case Data.NextNode.Maneuver: timeLabel = "Man"; break;
                        case Data.NextNode.Escape: timeLabel = "Esc"; break;
                    }
                    timeLabel = "T" + timeLabel + " -";
                    SetAndMark(TxtIdx.TNODE, timeLabel + FormatTime(data.timeToNode), MyStyleId.Plain);
                    if (data.nextNode == Data.NextNode.Ap || data.nextNode == Data.NextNode.Pe)
                    {
                        SetAndMark(TxtIdx.AP, "Ap " + FormatAltitude(data.apoapsis), data.nextNode == Data.NextNode.Ap ? MyStyleId.Emph : MyStyleId.Plain);
                        SetAndMark(TxtIdx.PE, "Pe " + FormatAltitude(data.periapsis), data.nextNode == Data.NextNode.Pe ? MyStyleId.Emph : MyStyleId.Plain);
                    }
                }

                // disable unmarked texts
                for (int i = 0; i < texts.Length; ++i)
                {
                    texts[i].enableGameObject = (markers[i] == markerMaster);
                }

                leftArea.UpdateLayout();
                rightArea.UpdateLayout();
            }

#if DEBUG
            if (Input.GetKeyDown(KeyCode.I))
            {
                GuiInfo guiInfo = GuiInfo.instance;
                guiInfo.Init();
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
                DMDebug dbg = new DMDebug();
                //dbg.PrintHierarchy(ScreenSafeUI.fetch);
                //dbg.PrintHierarchy(GameObject.Find("collapseExpandButton"));
                //dbg.PrintHierarchy(ScreenSafeUI.fetch.centerAnchor.bottom);
                dbg.PrintHierarchy(GameObject.Find("speedText"));
                dbg.Out("---------------------------------------------------------", 0);
                //dbg.PrintHierarchy(GameObject.Find("KFI-AREA-left"));
                //dbg.PrintHierarchy(GameObject.Find("KFI-AREA-right"));
                //dbg.Out("---------------------------------------------------------", 0);
                dbg.PrintHierarchy(GameObject.Find("UI camera"));
                dbg.Out("---------------------------------------------------------", 0);
                //dbg.PrintHierarchy(GameObject.Find("maneuverVector"));
                //int indent;
                //dbg.PrintGameObjectHierarchUp(ScreenSafeUI.fetch.centerAnchor.bottom.gameObject, out indent);
                //dbg.PrintGameObjectHierarchy(ScreenSafeUI.fetch.centerAnchor.bottom.gameObject, indent);
                //dbg.Out("---------------------------------------------------------", 0);
                dbg.PrintGameObjectHierarchy(leftArea.gameObject, 0);
                dbg.PrintGameObjectHierarchy(rightArea.gameObject, 0);
                dbg.Out("---------------------------------------------------------", 0);
                dbg.PrintGameObjectHierarchy(ScreenSafeUI.fetch.gameObject, 0);
                dbg.Out("---------------------------------------------------------", 0);
                var fonts = FindObjectsOfType(typeof(Font)) as Font[];
                foreach (Font font in fonts)
                    dbg.Out(font.name, 1);
                var f = KSP.IO.TextWriter.CreateForType<DMFlightData>("DMdebugoutput.txt", null);
                f.Write(dbg.ToString());
                f.Close();
            }
#endif
        }

        void SetupGUI()
        {
            DMDebug.Log2("SetupGUI");
            GuiInfo.instance.Init();

            leftArea = KFIArea.Create("left", new Vector2(GuiInfo.instance.screenAnchorLeft, 0f), TextAlignment.Right, null);
            rightArea = KFIArea.Create("right", new Vector2(GuiInfo.instance.screenAnchorRight, 0f), TextAlignment.Left, null);

            texts = new KFIText[9];
            texts[TxtIdx.ALT] = KFIText.Create("alt", MyStyleId.Emph);
            texts[TxtIdx.MACH] = KFIText.Create("mach", MyStyleId.Emph);
            texts[TxtIdx.AIR] = KFIText.Create("air", MyStyleId.Greyed);
            texts[TxtIdx.STALL] = KFIText.Create("stall", MyStyleId.Greyed);
            texts[TxtIdx.Q] = KFIText.Create("q", MyStyleId.Greyed);
            texts[TxtIdx.TEMP] = KFIText.Create("temp", MyStyleId.Greyed);
            texts[TxtIdx.TNODE] = KFIText.Create("tnode", MyStyleId.Plain);
            texts[TxtIdx.AP] = KFIText.Create("ap", MyStyleId.Plain);
            texts[TxtIdx.PE] = KFIText.Create("pe", MyStyleId.Plain);

            leftArea.Add(texts[TxtIdx.ALT]);
            leftArea.Add(texts[TxtIdx.TNODE]);
            leftArea.Add(texts[TxtIdx.AP]);
            leftArea.Add(texts[TxtIdx.PE]);
            rightArea.Add(texts[TxtIdx.MACH]);
            rightArea.Add(texts[TxtIdx.AIR]);
            rightArea.Add(texts[TxtIdx.STALL]);
            rightArea.Add(texts[TxtIdx.Q]);
            rightArea.Add(texts[TxtIdx.TEMP]);

            markers = new int[9];
        }

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
    }


}


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