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
            //if (name == "parent" || name == "root" || name == "target") return false;
            return true;
        }

        public void PrintGameObjectHierarchy(GameObject o, int indent)
        {
            Out(o.name + ", lp = " + o.transform.localPosition.ToString("F3") + ", p = " + o.transform.position.ToString("F3"), indent);
            //Out("[", indent);
            foreach (var comp in o.GetComponents<Component>())
            {
                Out("<"+comp.GetType().Name+" "+comp.name+">", indent);
            }
            //Out("]", indent);
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
                Out("{ " + instance.name + "(" + t.Name + ")", indent); //<" + instance.GetInstanceID() + ">"

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
                Out("}", indent);
            }
            catch (Exception e)
            {
                Out("Error: " + e.ToString(), indent);
            }
        }

        public override String ToString()
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
            Debug.Log("KerbalFlightData: " + s);
        }
        
        public static void LogWarning(string s)
        {
            Debug.LogWarning("KerbalFlightData: " + s);
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

        public static bool AlmostEqual(double a, double b, double eps)
        {
            return Math.Abs(a-b) < eps;
        }

        public static bool AlmostEqualRel(double a, double b, double eps)
        {
            return Math.Abs(a-b) <= (Math.Abs(a)+Math.Abs(b))*eps;
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
        public bool hasEnginePerf = false;
        public double totalThrust;

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
        public double smallestTempDifferenceFromCritical = 0;
        public bool hasTemp = false;
    };


    public delegate void VisitPartModule(Data data, Vessel vessel, Part part, PartModule module);
    public delegate void VisitPart(Data data, Vessel vessel, Part part);


    abstract class DataSource
    {
        public abstract void Update(Data data, Vessel vessel, ref VisitPartModule callbacksModule, ref VisitPart callbacksPart);
        public abstract void UpdatePostVisitation(Data data, Vessel vessel);
    };


    class DataFAR : DataSource
    {
        private Type FARControlSys = null;
        private bool farDataIsObtainedOkay = false;
        private FieldInfo fieldControlSys; // of FARControlSys
        private FieldInfo fieldQ, fieldMach, fieldAir, fieldStall;
        public bool obtainIntakeData = true;

        public DataFAR(Type FARControlSys_)
            : base()
        {
            FARControlSys = FARControlSys_;
            fieldControlSys = FARControlSys.GetField("activeControlSys", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var field in FARControlSys.GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.Name == "q")
                    fieldQ = field;
                else if (field.Name == "MachNumber")
                    fieldMach = field;
                else if (field.Name == "intakeDeficit")
                    fieldAir = field;
                else if (field.Name == "stallPercentage")
                    fieldStall = field;
            }
        }

        bool GetFARData(Data data)
        {
            var instance = fieldControlSys.GetValue(null);
            if (instance == null)  // maybe there is currently no active control system. That's okay ...
            {
                data.hasAerodynamics = false;
                data.hasAirAvailability = false;
                return false;
            }
            else
            {
                // any error here though, is a real error. It would probably mean that the assumptions about FARControlSys were invalidated by version updates.
                data.q = (double)fieldQ.GetValue(instance);
                data.machNumber = (double)fieldMach.GetValue(instance);
                data.airAvailability = obtainIntakeData ? (double)fieldAir.GetValue(null) : 1.0;
                data.stallPercentage = (double)fieldStall.GetValue(null);
                data.hasAerodynamics = true;
                data.hasAirAvailability = obtainIntakeData;
            }
            return true;
        }

        public override void Update(Data data, Vessel vessel, ref VisitPartModule callbacksModule, ref VisitPart callbacksPart)
        {
            bool ok = GetFARData(data);
            if (ok)
            {
                if (!farDataIsObtainedOkay)
                {
                    DMDebug.Log("Data from FAR obtained successfully");
                    farDataIsObtainedOkay = true;
                }
            }
            else
            {
                if (farDataIsObtainedOkay)
                {
                    DMDebug.Log("Failed to get data from FAR although it was obtained successfully before");
                    farDataIsObtainedOkay = false;
                }
                
            }
        }

        public override void UpdatePostVisitation(Data data, Vessel vessel)
        {
        }
    };


    class DataIntakeAirStock : DataSource
    {
        private double airDemand;
        private double airAvailable;
        PartResourceLibrary l = PartResourceLibrary.Instance;

        public override void Update(Data data, Vessel vessel, ref VisitPartModule callbacksModule, ref VisitPart callbacksPart)
        {
            data.hasAirAvailability = data.isInAtmosphere;
            if (!data.hasAirAvailability) return;

            airAvailable = 0;
            airDemand    = 0;

            callbacksModule += VisitPartModule;
        }

        public override void UpdatePostVisitation(Data data, Vessel vessel)
        {
            data.airAvailability = airAvailable / airDemand;
        }

        void VisitPartModule(Data data, Vessel vessel, Part part, PartModule m)
        {
            double fixedDeltaTime = TimeWarp.fixedDeltaTime;
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
    };


    class DataEnginePerformance : DataSource
    {
        public override void Update(Data data, Vessel vessel, ref VisitPartModule callbacksModule, ref VisitPart callbacksPart)
        {
            callbacksModule += VisitPartModule;
            data.hasEnginePerf = true;
            data.totalThrust = 0;
        }

        public override void UpdatePostVisitation(Data data, Vessel vessel)
        {
        }

        void VisitPartModule(Data data, Vessel vessel, Part part, PartModule m)
        {
            if (m is ModuleEngines)
            {
                ModuleEngines e = m as ModuleEngines;
                if (e.EngineIgnited && !e.engineShutdown)
                {
                    data.totalThrust += e.finalThrust;
                }
            }
            else if (m is ModuleEnginesFX)
            {
                ModuleEnginesFX e = m as ModuleEnginesFX;
                if (e.EngineIgnited && !e.engineShutdown)
                {
                    data.totalThrust += e.finalThrust;
                }
            }
        }
    };


    class DataSetupWarnings : DataSource
    {
        public override void Update(Data data, Vessel vessel, ref VisitPartModule callbacksModule, ref VisitPart callbacksPart)
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
                //if (data.highestRelativeTemp > 0.95)
                if (data.smallestTempDifferenceFromCritical < 50)
                    data.warnTemp = MyStyleId.Warn2;
                else if (data.smallestTempDifferenceFromCritical < 200)
                //else if (data.highestRelativeTemp > 0.80)
                    data.warnTemp = MyStyleId.Warn1;
                else
                    data.warnTemp = MyStyleId.Greyed;
            }
        }

        public override void UpdatePostVisitation(Data data, Vessel vessel)
        {
        }
    }



    class DataOrbitAndAltitude : DataSource
    {
        public override void Update(Data data, Vessel vessel, ref VisitPartModule callbacksModule, ref VisitPart callbacksPart)
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

        public override void UpdatePostVisitation(Data data, Vessel vessel)
        {
        }
    }



    class DataTemperature : DataSource
    {
        public override void Update(Data data, Vessel vessel, ref VisitPartModule callbacksModule, ref VisitPart callbacksPart)
        {
            data.hasTemp = data.isInAtmosphere;
            if (!data.hasTemp) return;

            callbacksPart += VisitPart;

            data.highestTemp = double.NegativeInfinity;
            data.highestRelativeTemp = double.NegativeInfinity;
            data.smallestTempDifferenceFromCritical = double.PositiveInfinity;
        }

        public override void UpdatePostVisitation(Data data, Vessel vessel)
        {
        }

        void VisitPart(Data data, Vessel vessel, Part p)
        {
            if (p.temperature != 0f) // small gear box has p.temperature==0 - always! Bug? Who knows. Anyway i want to ignore it.
            {
                float dreTempThreshold = p.maxTemp;
                bool is_engine = (p.Modules.Contains("ModuleEngines") || p.Modules.Contains("ModuleEnginesFX"));
                if (is_engine)
                    dreTempThreshold *= 0.975f;
                else
                    dreTempThreshold *= 0.85f;
                    if (dreTempThreshold - p.temperature < data.smallestTempDifferenceFromCritical)
                {
                    data.smallestTempDifferenceFromCritical = dreTempThreshold - p.temperature;
                    data.highestTemp = p.temperature;
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


    public struct KFDContent
    {
        public KFDContent(string text_, int styleId_)
        {
            this.text = text_;
            this.styleId = styleId_;
        }
        public readonly string text;
        public readonly int styleId;
    };


    /* this scripts manages a piece of text. It allocates the main text plus some clones for shadowing. 
     * The clones get a dark color and are drawn behind the main text with a slight offset */
    public class KFDText : MonoBehaviour
    {
        // GUITexts are positioned in viewport space [0,1]^2
        GUIText    gt1_; 
        GUIText    gt2_; // child of gt1_
        GUIText    gt3_; // child of gt1_
        int        styleId_ = -1;
        Rect       screenRect_;
        int change_ = 0, last_change_checked_ = -1; // with this i check if the screen space rect must be recomputed
        Func<Data, KFDContent> getContent_;
        Func<Data, bool> hasChanged_;

        public static KFDText Create(string id, int styleId, Func<Data, KFDContent> getContent, Func<Data, bool> hasChanged)
        {
            // foreground text
            GameObject textGO = new GameObject("KFD-" + id);
            textGO.layer = 12; // navball layer

            KFDText kfi = textGO.AddComponent<KFDText>();
            
            kfi.gt1_ = textGO.AddComponent<GUIText>();
            kfi.gt1_.anchor = TextAnchor.LowerLeft;
            kfi.gt1_.alignment = TextAlignment.Left;

            // background shadow 1
            GameObject shadowGO = new GameObject("KFD-SH-" + id);            
            shadowGO.layer = 12;
            shadowGO.transform.parent = textGO.transform;
            shadowGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);

            kfi.gt2_ = shadowGO.AddComponent<GUIText>();
            kfi.gt2_.anchor = TextAnchor.LowerLeft;
            kfi.gt2_.alignment = TextAlignment.Left;

            // background shadow 2
            shadowGO = new GameObject("KFD-SH2-" + id);
            shadowGO.layer = 12;
            shadowGO.transform.parent = textGO.transform;
            shadowGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);

            kfi.gt3_ = shadowGO.AddComponent<GUIText>();
            kfi.gt3_.anchor = TextAnchor.LowerLeft;
            kfi.gt3_.alignment = TextAlignment.Left;

            kfi.getContent_ = getContent;
            kfi.hasChanged_ = hasChanged;

            return kfi;
        }
        

        public void UpdateText(Data data)
        {
            if (hasChanged_(data))
            {
                //DMDebug.Log2(name + " has changed");
                KFDContent c = getContent_(data);

                if (this.styleId_ != c.styleId)  // careful because of potentially costly update
                {
                    this.styleId_ = c.styleId;
                    GUIStyle s = GuiInfo.instance.styles[c.styleId];
                    this.gt1_.fontStyle = s.fontStyle;
                    this.gt1_.fontSize = GuiInfo.instance.fontSize;
                    this.gt1_.font = s.font;
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
                }                
                // not going to compare here
                this.gt1_.text = c.text;
                this.gt2_.text = c.text;
                this.gt3_.text = c.text;
                ++change_;
            }
            //else
                //DMDebug.Log2(name + " unchanged");
        }


        public void OnDestroy()
        {
            DMDebug.Log2(this.name + " OnDestroy");
            // release links to make it easier for the gc
            gt1_ = null;
            gt2_ = null;
            gt3_ = null;
            hasChanged_ = null;
            getContent_ = null;
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

    
    public enum VerticalAlignment 
    {
        Top = 1,
        Bottom = -1
    };


    /* This class represents the left/right text areas. Texts are managed as children (by Unity GameObjects).
     * The point is to have the area auto-expand the contain one text per row in the vertical and 
     * to auto-expand in width and to align the text */
    public class KFDArea : MonoBehaviour
    {
        public TextAlignment alignment;
        public VerticalAlignment verticalAlignment = VerticalAlignment.Bottom;
        public List<KFDText> items;
        float maximalWidth = 0f;

        /* note: alignment is actually the alignment of the area. e.g. right-alignment means that the anchor point
         * is at the right bottom(!) corner. The text inside is always left-aligned*/
        public static KFDArea Create(string id, Vector2 position_, TextAlignment alignment_, Transform parent) // factory, creates game object with attached FKIArea
        {
            GameObject go = new GameObject("KFD-AREA-"+id);
            KFDArea kfi = go.AddComponent<KFDArea>();
            kfi.alignment = alignment_;
            kfi.useGUILayout = false;
            kfi.items = new List<KFDText>();
            go.transform.parent = parent;
            go.transform.localPosition = position_;
            return kfi;
        }

        public void UpdateLayout() // called from the KerbalFlightData script
        {
            //DMDebug.Log2(this.name + " LateUpdate");
            float w = maximalWidth, h = 0;
            foreach (KFDText t in items)
            {
                if (!t || !t.enableGameObject) continue;
                Rect r = t.screenRect;
                w = Mathf.Max(w, r.width);
                h += r.height;
            }
            Vector2 p = Vector2.zero; // the current position of the child texts
            if (alignment == TextAlignment.Right) p.x -= w;
            if (verticalAlignment == VerticalAlignment.Bottom)
                p.y += h;
            foreach (KFDText t in items)
            {
                if (!t || !t.enableGameObject) continue;
                p.y -= t.screenRect.height;
                t.localPosition = p;
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

        public void Add(KFDText t)
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

        int timeSecondsPerDay;
        int timeSecondsPerYear;

        const float navballWidth = 0.072f;
        const float navballGaugeWidth = 0.030f;
        const float navballGaugeWidthNonscaling = 0.030f;
        //const float baselineFontSize = 16; 
        float baseFontSizeIVA = 16; // font size @ "normal" UI scale setting
        float baseFontSizeExternal = 16;

        public Camera camera = null;
        public int   fontSize;
        public float screenAnchorRight;
        public float screenAnchorLeft;
        public float screenAnchorVertical;
        public bool  ready = false;
        public bool  anchorTop = false;

        public GUIStyle prototypeStyle;
        public GUIStyle[] styles = { null, null, null, null, null };

        public void LoadSettings(ConfigNode settings)
        {
            if (settings.HasValue("baseFontSizeIVA")) baseFontSizeIVA  = float.Parse(settings.GetValue("baseFontSizeIVA"));
            if (settings.HasValue("baseFontSizeExternal")) baseFontSizeExternal  = float.Parse(settings.GetValue("baseFontSizeExternal"));
        }

        public void SaveSettings(ConfigNode settings)
        {
            settings.AddValue("baseFontSizeIVA", baseFontSizeIVA);
            settings.AddValue("baseFontSizeExternal", baseFontSizeExternal);
        }

        public void Init()
        {
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

            GameObject go = GameObject.Find("speedText");
            prototypeStyle = go.GetComponent<ScreenSafeGUIText>().textStyle;

            // GUI functions must only be called in OnGUI ... but ... you can apparently still clone styles ...
            var s = new GUIStyle(prototypeStyle);
            s.SetColor(Color.white);
            s.padding = new RectOffset(0, 0, 0, 1);
            s.margin = new RectOffset(0, 0, 0, 0);
            s.fontSize = Mathf.RoundToInt(baseFontSizeExternal * uiScalingFactor);
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
            uiScalingFactor *= ((float)Screen.height)/Screen.width*1920f/1080f;
            uiScalingFactor *= anchorScale; // scale font with navball
            //DMDebug.Log2("uiScalingFactor = " + uiScalingFactor + "\n" +
            //             "1/aspect = " + (((float)Screen.height) / Screen.width) + "\n" +
            //             "orthoSize = " + camera.orthographicSize);
            
            bool isIVA = CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA;
            if (isIVA)
            {
                Vector3 p = camera.WorldToViewportPoint(ScreenSafeUI.fetch.centerAnchor.top.transform.position);
                screenAnchorVertical = p.y - (10f/Screen.height);
                float offset = 10f/Screen.width;
                screenAnchorLeft   = p.x - offset;
                screenAnchorRight  = p.x + offset;
                anchorTop = true;
                fontSize = Mathf.RoundToInt(baseFontSizeIVA * uiScalingFactor);
            }
            else
            {
                // update the anchor positions and the required font size
                Vector3 p = camera.WorldToViewportPoint(navballGameObject.transform.position);
                bool hasGauge = burnVector_.deltaVGauge != null && burnVector_.deltaVGauge.gameObject.activeSelf == true;
                if (hasGauge)
                    screenAnchorRight = p.x + navballWidth * uiScalingFactor + navballGaugeWidth * uiScalingFactor + navballGaugeWidthNonscaling;
                else
                    screenAnchorRight = p.x + navballWidth * uiScalingFactor;            
                screenAnchorLeft   = p.x - navballWidth * uiScalingFactor;
                screenAnchorVertical = p.y;
                anchorTop = false;
                fontSize = Mathf.RoundToInt(baseFontSizeExternal * uiScalingFactor);
            }
            
        }

        public static GuiInfo instance
        {
            get { return instance_; }
        }
        
        #region GUIUtil
        public static String FormatPressure(double x)
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

        public static String FormatAltitude(double x)
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


        public static String FormatRadarAltitude(double x)
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


        public static String FormatTime(double x_)
        {
            const int MIN = 60;
            const int H = 3600;
            int D = instance.timeSecondsPerDay;
            int Y = instance.timeSecondsPerYear;

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

    #endregion


    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class DMFlightData : MonoBehaviour
    {
        double dtSinceLastUpdate;

        bool displayUIByGuiEvent = true;
        bool displayUIByToolbarClick = true;

        Data data;
        List<DataSource> dataSources = new List<DataSource>();
        VisitPart partVisitors;
        VisitPartModule moduleVisitors;

        static IButton toolbarButton;

        // gui stuff
        KFDText[] texts = null;
        int[] markers = null;
        int markerMaster = 0;
        KFDArea leftArea, rightArea;
        static class TxtIdx
        {
            public const int MACH = 0, ALT = 1, AIR = 2, STALL = 3, Q = 4, TEMP = 5, TNODE = 6, AP = 7, PE = 8, ENGINEPERF = 9, COUNT = 10;
        };

        void Awake() // Awake is called when the script instance is being loaded.
        {
            DMDebug.Log2(name + " Awake!");
            dataSources.Clear();
            DataFAR farData = null;
            bool hasAJE = false;
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
                            farData = new DataFAR(t);
                        }
                    }
                }
                else if (assembly.name == "DeadlyReentry")
                {
                    dataSources.Add(new DataTemperature());
                }
                else if (assembly.name == "AJE")
                {
                    hasAJE = true;
                }
            }
            if (farData == null)
                dataSources.Add(new DataIntakeAirStock());
            else
            {
                farData.obtainIntakeData = hasAJE == false;
                dataSources.Add(farData);
            }
            if (hasAJE)
                dataSources.Add(new DataEnginePerformance());
            dataSources.Add(new DataOrbitAndAltitude());
            dataSources.Add(new DataSetupWarnings());

            data = new Data();

            if (ToolbarManager.ToolbarAvailable)
            {
                toolbarButton = ToolbarManager.Instance.add("KerbalFlightData", "damichelsflightdata");
                toolbarButton.TexturePath = "KerbalFlightData/toolbarbutton";
                toolbarButton.ToolTip = "KerbalFlightData On/Off Switch";
                toolbarButton.Visibility = new GameScenesVisibility(GameScenes.FLIGHT);
                toolbarButton.Enabled = true;
                toolbarButton.OnClick += (e) =>
                {
                    displayUIByToolbarClick = !displayUIByToolbarClick;
                    UpdateEnabling();
                };
            }

            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);

            LoadSettings();
            UpdateEnabling(); // might start with it disabled
        }


        void OnHideUI()
        {
            displayUIByGuiEvent = false;
            UpdateEnabling();
        }


        void OnShowUI()
        {
            displayUIByGuiEvent = true;
            UpdateEnabling();
        }


        void UpdateEnabling()
        {
            enabled = displayUIByToolbarClick && displayUIByGuiEvent;
        }


        void SaveSettings()
        {
            ConfigNode settings = new ConfigNode();
            settings.name = "SETTINGS";
            settings.AddValue("active", displayUIByToolbarClick);
            GuiInfo.instance.SaveSettings(settings);
            settings.Save(AssemblyLoader.loadedAssemblies.GetPathByType(typeof(DMFlightData)) + "/settings.cfg");
        }


        void LoadSettings()
        {
            ConfigNode settings = new ConfigNode();
            settings = ConfigNode.Load(AssemblyLoader.loadedAssemblies.GetPathByType(typeof(DMFlightData)) + "/settings.cfg");
            if (settings != null)
            {
                if (settings.HasValue("active")) displayUIByToolbarClick = bool.Parse(settings.GetValue("active"));
                GuiInfo.instance.LoadSettings(settings);
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

            dataSources.Clear();

            // unregister, or else errors occur
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);

            if (toolbarButton != null)
                toolbarButton.Destroy();
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
            
            if (!FlightUIModeController.Instance.navBall.enabled) return false;

            return true;
        }


        bool CheckFullUpdateTimer()
        {
            if (dtSinceLastUpdate > 0.1) // update every so and so fraction of a second
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

        void LateUpdate()
        {
            dtSinceLastUpdate += Time.unscaledDeltaTime;

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
                leftArea.transform.localPosition = new Vector2(guiInfo.screenAnchorLeft, guiInfo.screenAnchorVertical);
                rightArea.transform.localPosition = new Vector2(guiInfo.screenAnchorRight, guiInfo.screenAnchorVertical);
                leftArea.verticalAlignment = guiInfo.anchorTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                rightArea.verticalAlignment = guiInfo.anchorTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            }

            if (on && CheckFullUpdateTimer())
            {
                // obtain data
                Vessel vessel = FlightGlobals.ActiveVessel;
                foreach (DataSource d in dataSources)
                {
                    d.Update(data, vessel, ref moduleVisitors, ref partVisitors);
                }
                if (partVisitors != null || moduleVisitors != null)
                {
                    int iCnt = vessel.parts.Count;
                    for (int i = 0; i < iCnt; ++i)
                    {
                        Part p = vessel.parts[i];
                        if (p == null) 
                            continue;
                        if (partVisitors != null)
                            partVisitors(data, vessel, p);
                        if (moduleVisitors != null)
                        {
                            int jCnt = p.Modules.Count;
                            for (int j = 0; j < jCnt; ++j)
                            {
                                PartModule m = p.Modules[j];
                                if (m == null) 
                                    continue;
                                moduleVisitors(data, vessel, p, m);
                            }
                        }
                    }
                }
                foreach (DataSource d in dataSources)
                {
                    d.UpdatePostVisitation(data, vessel);
                }
                partVisitors = null;
                moduleVisitors = null;
                // done with getting data

                ++markerMaster;

                if (data.isInAtmosphere && data.hasEnginePerf)
                {
                    markers[TxtIdx.ENGINEPERF] = markerMaster;
                }

                if (data.isInAtmosphere && !data.isLanded)
                {
                    if (data.hasAerodynamics)
                    {
                        markers[TxtIdx.MACH] = markerMaster;

                    }
                    if (data.hasAirAvailability)
                    {
                        markers[TxtIdx.AIR] = markerMaster;
                    }
                    if (data.hasAerodynamics)
                    {
                        markers[TxtIdx.Q] = markerMaster;
                        markers[TxtIdx.STALL] = markerMaster;

                    }
                    if (data.hasTemp)
                    {
                        markers[TxtIdx.TEMP] = markerMaster;
                    }
                }
                markers[TxtIdx.ALT] = markerMaster;
                if (data.isAtmosphericLowLevelFlight == false)
                {
                    markers[TxtIdx.TNODE] = markers[TxtIdx.AP] = markers[TxtIdx.PE] = markerMaster;
                }

                // disable unmarked texts, update marked ones
                for (int i = 0; i < texts.Length; ++i)
                {
                    texts[i].enableGameObject = (markers[i] == markerMaster);
                    if (markers[i] == markerMaster)
                        texts[i].UpdateText(data);
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

                dbg.Out("---------------------------------------------------------", 0);
                dbg.PrintGameObjectHierarchy(InternalSpace.Instance.gameObject, 0);
                dbg.Out("---------------------------------------------------------", 0);
                //InternalSpeed spd = InternalSpeed.FindObjectsOfType<InternalSpeed>().FirstOrDefault();
                //dbg.Out(spd.textObject.text.text, 0);
                //dbg.Out("---------------------------------------------------------", 0);
                //dbg.PrintHierarchy(spd);
                //dbg.PrintHierarchy(InternalCamera.Instance);
                //dbg.PrintHierarchy(FlightGlobals.ActiveVessel);
                //dbg.PrintHierarchy(GameObject.Find("collapseExpandButton"));
                //dbg.PrintHierarchy(ScreenSafeUI.fetch.centerAnchor.bottom);
                //dbg.PrintHierarchy(GameObject.Find("speedText"));
                //dbg.Out("---------------------------------------------------------", 0);
                //dbg.PrintHierarchy(GameObject.Find("KFD-AREA-left"));
                //dbg.PrintHierarchy(GameObject.Find("KFD-AREA-right"));
                //dbg.Out("---------------------------------------------------------", 0);
                //dbg.PrintHierarchy(GameObject.Find("UI camera"));
                //dbg.Out("---------------------------------------------------------", 0);
                //dbg.PrintHierarchy(GameObject.Find("maneuverVector"));
                //int indent;
                //dbg.PrintGameObjectHierarchUp(ScreenSafeUI.fetch.centerAnchor.bottom.gameObject, out indent);
                //dbg.PrintGameObjectHierarchy(ScreenSafeUI.fetch.centerAnchor.bottom.gameObject, indent);
                //dbg.Out("---------------------------------------------------------", 0);
                //dbg.PrintGameObjectHierarchy(leftArea.gameObject, 0);
                //dbg.PrintGameObjectHierarchy(rightArea.gameObject, 0);
                //dbg.Out("---------------------------------------------------------", 0);
                //dbg.PrintGameObjectHierarchy(ScreenSafeUI.fetch.gameObject, 0);
                //dbg.Out("---------------------------------------------------------", 0);
                var fonts = FindObjectsOfType(typeof(Font)) as Font[];
                foreach (Font font in fonts)
                    dbg.Out(font.name, 1);
                var f = KSP.IO.TextWriter.CreateForType<DMFlightData>("DMdebugoutput.txt", null);
                f.Write(dbg.ToString());
                f.Close();
            }
#endif
        }

        static bool SetIf<T1>(ref T1 dst, T1 src, bool b)
        {
            if (b) { dst = src; return true; }
            else return false;
        }

        static bool SetIf<T1, T2>(ref T1 dst1, ref T2 dst2, T1 src1, T2 src2, bool b)
        {
            if (b) { 
                dst1 = src1; 
                dst2 = src2;
                return true; 
            }
            else return false;
        }

        void SetupGUI()
        {
            DMDebug.Log2("SetupGUI");
            GuiInfo.instance.Init();

            leftArea = KFDArea.Create("left", new Vector2(GuiInfo.instance.screenAnchorLeft, 0f), TextAlignment.Right, null);
            rightArea = KFDArea.Create("right", new Vector2(GuiInfo.instance.screenAnchorRight, 0f), TextAlignment.Left, null);
            texts = new KFDText[TxtIdx.COUNT];

            double tmpMach = -1;
            texts[TxtIdx.MACH] = KFDText.Create("mach", MyStyleId.Emph,
                (Data d) => new KFDContent("Mach " + d.machNumber.ToString("F2"), MyStyleId.Emph),
                (Data d) => SetIf(ref tmpMach, d.machNumber, !Util.AlmostEqual(d.machNumber, tmpMach, 0.01)));

            double tmpAir = -1;
            texts[TxtIdx.AIR] = KFDText.Create("air", MyStyleId.Greyed,
                (Data d) => new KFDContent("Intake" + (d.airAvailability < 2 ? "  " + (d.airAvailability * 100d).ToString("F0") + "%" : ""), d.warnAir),
                (Data d) => SetIf(ref tmpAir, d.airAvailability, (d.airAvailability < 2.1 || tmpAir<2.1) ? !Util.AlmostEqual(d.airAvailability, tmpAir, 0.01) : false));

            double tmpEngPerf = -1;
            texts[TxtIdx.ENGINEPERF] = KFDText.Create("eng", MyStyleId.Greyed,
                (Data d) => new KFDContent("Thr. " + d.totalThrust.ToString("F0") + " kN", MyStyleId.Greyed), 
                (Data d) => SetIf(ref tmpEngPerf, d.totalThrust, !Util.AlmostEqual(d.totalThrust, tmpEngPerf, 0.5)));

            int tmpStall = -1;
            texts[TxtIdx.STALL] = KFDText.Create("stall", MyStyleId.Greyed,
                (Data d) => new KFDContent("Stall", d.warnStall),
                (Data d) => SetIf(ref tmpStall, d.warnStall, d.warnStall != tmpStall));

            double tmpQ = -1;
            texts[TxtIdx.Q] = KFDText.Create("q", MyStyleId.Greyed,
                (Data d) => new KFDContent("Q  " + GuiInfo.FormatPressure(d.q), d.warnQ),
                (Data d) => SetIf(ref tmpQ, d.q, !Util.AlmostEqualRel(d.q, tmpQ, 0.01)));

            double tmpTemp = -1;
            int    tmpWarnTemp = -1;
            texts[TxtIdx.TEMP] = KFDText.Create("temp", MyStyleId.Greyed,
                (Data d) => new KFDContent("T " + d.highestTemp.ToString("F0") + " °C", d.warnTemp),
                (Data d) => SetIf(ref tmpTemp, ref tmpWarnTemp, d.highestTemp, d.warnTemp, !Util.AlmostEqual(d.highestTemp, tmpTemp, 1) || d.warnTemp != tmpWarnTemp));


            double tmpAlt = -1;
            texts[TxtIdx.ALT] = KFDText.Create("alt", MyStyleId.Emph,
                (Data d) => new KFDContent("Alt " + (d.radarAltitude < 5000 ? (GuiInfo.FormatRadarAltitude(d.radarAltitude) + " R") : GuiInfo.FormatAltitude(d.altitude)), d.radarAltitude < 200 ? MyStyleId.Warn1 : MyStyleId.Emph),
                (Data d) => SetIf(ref tmpAlt, d.altitude, !Util.AlmostEqualRel(d.altitude, tmpAlt, 0.001)));


            Func<Data, KFDContent> fmtTime = (Data d) =>
            {
                String timeLabel = "";
                switch (d.nextNode)
                {
                    case Data.NextNode.Ap: timeLabel = "Ap"; break;
                    case Data.NextNode.Pe: timeLabel = "Pe"; break;
                    case Data.NextNode.Encounter: timeLabel = "En"; break;
                    case Data.NextNode.Maneuver: timeLabel = "Man"; break;
                    case Data.NextNode.Escape: timeLabel = "Esc"; break;
                }
                timeLabel = "T" + timeLabel + " -" + GuiInfo.FormatTime(d.timeToNode);
                return new KFDContent(timeLabel, MyStyleId.Plain);
            };

            double tmpTNode = -1;
            texts[TxtIdx.TNODE] = KFDText.Create("tnode", MyStyleId.Plain,
                fmtTime,
                (Data d) => SetIf(ref tmpTNode, d.timeToNode, !Util.AlmostEqual(d.timeToNode, tmpTNode, 1)));

            Data.NextNode tmpNextNode1 = Data.NextNode.Maneuver;
            double tmpAp = -1;
            texts[TxtIdx.AP] = KFDText.Create("ap", MyStyleId.Plain,
                (Data d) => new KFDContent("Ap " + GuiInfo.FormatAltitude(data.apoapsis), data.nextNode == Data.NextNode.Ap ? MyStyleId.Emph : MyStyleId.Plain),
                (Data d) => SetIf(ref tmpAp, ref tmpNextNode1, d.apoapsis, d.nextNode, !Util.AlmostEqualRel(d.apoapsis, tmpAp, 0.001) || d.nextNode != tmpNextNode1));

            Data.NextNode tmpNextNode2 = Data.NextNode.Maneuver;
            double tmpPe = 0;
            texts[TxtIdx.PE] = KFDText.Create("pe", MyStyleId.Plain,
                (Data d) => new KFDContent("Pe " + GuiInfo.FormatAltitude(data.periapsis), data.nextNode == Data.NextNode.Pe ? MyStyleId.Emph : MyStyleId.Plain),
                (Data d) => SetIf(ref tmpPe, ref tmpNextNode2, d.periapsis, d.nextNode, !Util.AlmostEqualRel(d.periapsis, tmpPe, 0.001) || d.nextNode != tmpNextNode2));

            leftArea.Add(texts[TxtIdx.ALT]);
            leftArea.Add(texts[TxtIdx.TNODE]);
            leftArea.Add(texts[TxtIdx.AP]);
            leftArea.Add(texts[TxtIdx.PE]);
            rightArea.Add(texts[TxtIdx.MACH]);
            rightArea.Add(texts[TxtIdx.AIR]);
            rightArea.Add(texts[TxtIdx.ENGINEPERF]);
            rightArea.Add(texts[TxtIdx.STALL]);
            rightArea.Add(texts[TxtIdx.Q]);
            rightArea.Add(texts[TxtIdx.TEMP]);

            markers = new int[TxtIdx.COUNT];
        }


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