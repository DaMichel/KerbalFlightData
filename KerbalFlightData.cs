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
using System.ComponentModel;
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
        HashSet<int> visited = new HashSet<int>();

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
            return true;
        }

        public void PrintGameObjectHierarchy(GameObject o, int indent)
        {
            Out(o.name + ", lp = " + o.transform.localPosition.ToString("F3") + ", p = " + o.transform.position.ToString("F3") + ", s = " + o.transform.localScale.ToString("F1") + ", en = " + o.activeSelf.ToString(), indent);
            var rt = o.GetComponent<UnityEngine.RectTransform>();
            if (rt)
            {
                Out(String.Format(", rtlp = {0}, rtp = {1}, rect = {2}", rt.localPosition.ToString("F3"), rt.position.ToString("F3"), rt.rect.ToString("F2")), indent + o.name.Length);
            }
            //Out("[", indent);
            foreach (var comp in o.GetComponents<UnityEngine.Component>())
            {
                if (rt && comp == rt)
                    continue;
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

        public void PrintHierarchy(UnityEngine.Object instance, int indent = 0, bool recursive = true)
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
                    if (IsOkayToExpand(field.Name, field.FieldType) && recursive)
                    {
                        PrintHierarchy((UnityEngine.Object)value, indent + 2, recursive);
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
                    if (IsOkayToExpand(prop.Name, prop.PropertyType) && recursive)
                    {
                        PrintHierarchy((UnityEngine.Object)value, indent + 2, recursive);
                    }
                }

                if (typeof(GameObject).IsAssignableFrom(t))
                {
                    Out("[Components of " + instance.name + " ]", indent + 1);
                    foreach (var comp in ((GameObject)instance).GetComponents<UnityEngine.Component>())
                    {
                        PrintHierarchy(comp, indent + 2, recursive);
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

        public static GameObject GetRoot(GameObject o)
        {
            while (o.transform.parent != null)
            {
                o = o.transform.parent.gameObject;
            }
            return o;
        }

        public static String ToString(object o)
        {
            if (o == null)
                return "null";
            return o.ToString();
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

        public static void TryReadValue<T>(ref T target, ConfigNode node, string name)
        {
            if (node.HasValue(name))
            {
                try
                {
                    target = (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(node.GetValue(name));
                }
                catch
                {
                    // just skip over it
                }
            }
            // skip again
        }

        // point from frame a to the corresponding point in frame b
        public static Vector2 TransformPoint(Transform a, Transform b, float x, float y)
        {
            var p = a.TransformPoint(x, y, 0);
            p = b.InverseTransformPoint(p);
            return new Vector2(p.x, p.y);
        }

        public static Vector2 TransformPoint(Transform a, Transform b, Vector2 p)
        {
            return TransformPoint(a, b, p.x, p.y);
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
    public bool hasAerodynamics = false; // mach, q
    public bool hasStalls = false;
    public bool hasEnginePerf = false;
    public double airBreatherThrust;
    public double throttle;
    public double totalThrust;
    public bool hasAirBreathingEngines; // also if they are active

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
    public bool isDisplayingRadarAlt;

    public double altitude = 0;
    public double radarAltitude = 0;
    public double verticalSpeed = 0;
    public double radarAltitudeDeriv = 0;
    public double timeToImpact = 0;

    public double highestTemp = 0;
    public double highestTempMax = 0;
    public double tempWarnMetric = 0;
    public bool   highestTempIsSkinTemp = false;
    public bool hasTemp = false;
};


class DataFAR
{
    private static Type FARAPI = null;
    private static bool farDataIsObtainedOkay = false;
    private static MethodInfo VesselStallFrac;
    private static MethodInfo VesselDynPres;
    private static MethodInfo VesselFlightInfo;
    public  static bool obtainIntakeData = true;

    public static void Init(Type FARAPI_)
    {
        FARAPI = FARAPI_;
        DMDebug.Log2(String.Format("FARAPI = {0}", FARAPI.ToString()));
        foreach (var method in FARAPI.GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
        {
            DMDebug.Log2(String.Format("method = {0}", method.Name));
            if (method.Name == "VesselStallFrac")
                VesselStallFrac = method;
            else if (method.Name == "VesselDynPres")
                VesselDynPres = method;
            else if (method.Name == "VesselFlightInfo")
                VesselFlightInfo = method;
        }
    }

    private static bool GetFARData_Internal(Data data, Vessel vessel)
    {
        var arg = new object[] {vessel}; 
        object instance = VesselFlightInfo.Invoke(null, arg);  // this looks stupidly costly, but what can we do?!
        //DMDebug.Log2("FAR seems to be " + ((instance == null) ? "not " : "") + "ready");
        if (instance == null) 
        {
            data.hasAerodynamics = false;
            data.hasStalls = false;
            //data.hasAirAvailability = false;
            return false;
        }
        else
        {
            //DMDebug.Log2("q");
            // any error here though, is a real error. It would probably mean that the assumptions about FARControlSys were invalidated by version updates.
            //data.q = (double)VesselDynPres.Invoke(null, arg);
            data.q = vessel.dynamicPressurekPa * 1000.0;
            //DMDebug.Log2("m");
            data.machNumber = vessel.mach;
            //data.airAvailability = obtainIntakeData ? (double)fieldAir.GetValue(null) : 1.0;
            //DMDebug.Log2("stall");
            data.stallPercentage = (double)VesselStallFrac.Invoke(null, arg);
            data.hasAerodynamics = true;
            data.hasStalls = true;
            //data.hasAirAvailability = obtainIntakeData;
        }
        return true;
    }

    public static bool GetFARData(Data data, Vessel vessel)
    {
        bool ok = GetFARData_Internal(data, vessel);
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
        return ok;
    }
};


class DataSources
{
    private static PartResourceLibrary l = PartResourceLibrary.Instance;
    private static bool hasFAR  = false;
    //private static bool hasDre            = false;
    //private static bool hasAJE            = false;

    private static double airDemand = 0;
    private static double airAvailable = 0;
    
    private static bool hasEngine = false;
    private static bool lastPartIsEngine = false;
    private static bool needsTemp = false;
    private const double tempWarnThreshold1 = 0.5;
    private const double tempWarnThreshold2 = 0.2;
    private const double tempWarnThreshold3 = 0.05;

    public static void Init()
    {
        foreach (var assembly in AssemblyLoader.loadedAssemblies)
        {
            //DMDebug.Log2(assembly.name);
            if (assembly.name == "FerramAerospaceResearch")
            {
                var types = assembly.assembly.GetExportedTypes();
                foreach (Type t in types)
                {
                    //DMDebug.Log2(t.FullName);
                    if (t.FullName.Equals("FerramAerospaceResearch.FARAPI"))
                    {
                        DataFAR.Init(t);
                        hasFAR = true;
                    }
                }
            }
            //else if (assembly.name == "DeadlyReentry")
            //{
            //    hasDre = true;
            //}
            //else if (assembly.name == "AJE")
            //{
            //    hasAJE = true;
            //}
        }
    }

    /*
     * collects engine data, intake air and determines if the part is an engine.
     */
    private static void VisitModule(Data data, PartModule m, Part p, Vessel vessel)
    {
        double fixedDeltaTime = TimeWarp.fixedDeltaTime;
        if (m is ModuleEngines)
        {
            ModuleEngines e = m as ModuleEngines;
            if (e.EngineIgnited && !e.engineShutdown)
            {
                bool needsAir = false;
                //if (obtainIntakeAir || ) // don't iterate propellants if we don't have to
                {
                    foreach (Propellant v in e.propellants)
                    {
                        string propName = v.name;
                        PartResourceDefinition r = l.resourceDefinitions[propName];
                        if (propName == "IntakeAir")
                        {
                            airDemand += v.currentRequirement;
                            needsAir = true;
                            continue;
                        }
                    }
                }
                if (needsAir)
                {
                    data.airBreatherThrust += e.finalThrust;
                    data.hasAirBreathingEngines = true;
                 }
                data.totalThrust += e.finalThrust;
                lastPartIsEngine = true;
            }
        }
        else if (m is ModuleEnginesFX)
        {
            ModuleEnginesFX e = m as ModuleEnginesFX;
            if (e.EngineIgnited && !e.engineShutdown)
            {
                bool needsAir = false;
                //if (obtainIntakeAir) // don't iterate propellants if we don't have to
                {
                    foreach (Propellant v in e.propellants)
                    {
                        string propName = v.name;
                        PartResourceDefinition r = l.resourceDefinitions[propName];
                        if (propName == "IntakeAir")
                        {
                            airDemand += v.currentRequirement;
                            needsAir = true;
                            continue;
                        }
                    }
                }
                if (needsAir)
                {
                    data.airBreatherThrust += e.finalThrust;
                    data.hasAirBreathingEngines = true;
                }
                data.totalThrust += e.finalThrust;
                lastPartIsEngine = true;
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


    private static void FillLocationData(Data data, Vessel vessel)
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
                double hmin = (double)(((int)(b.atmosphereDepth * 0.33333333e-3))) * 1000;
                data.isAtmosphericLowLevelFlight = !(data.apoapsis > hmin || data.periapsis > hmin);
            }
            else
                data.isAtmosphericLowLevelFlight = false;
            data.isInAtmosphere = b.atmosphere && vessel.altitude < b.atmosphereDepth;
        }

        data.altitude = vessel.altitude;
        // for data.radarAltitude see FixedUpdate()
        data.verticalSpeed = vessel.verticalSpeed;

        data.timeToImpact = data.radarAltitudeDeriv < 0 ? -data.radarAltitude/data.radarAltitudeDeriv : double.PositiveInfinity;

        data.isLanded = false;
        if (vessel.LandedOrSplashed)
        {
            double srfSpeedSqr = vessel.GetSrfVelocity().sqrMagnitude;
            if (srfSpeedSqr < 0.01)
                data.isLanded = true;
        }
    }

    private static void UpdateWarningIndicators(Data data)
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
            if (data.tempWarnMetric < tempWarnThreshold3)
                data.warnTemp = MyStyleId.Warn2;
            else if (data.tempWarnMetric < tempWarnThreshold2)
                data.warnTemp = MyStyleId.Warn1;
            else if (data.tempWarnMetric < tempWarnThreshold1)
                data.warnTemp = MyStyleId.Emph;
            else
                data.warnTemp = MyStyleId.Greyed;
        }
    }


    public static void FillDataInstance(Data data, Vessel vessel)
    {
        // air for the engines
        airAvailable = 0;
        airDemand    = 0;
        // engine perf
        data.totalThrust = 0;
        data.airBreatherThrust = 0;
        data.hasAirBreathingEngines = false;
        // location, put stuff in data, need for further processing if we are atmospheric
        FillLocationData(data, vessel);
        // temp
        needsTemp = data.isInAtmosphere;
        data.highestTemp = double.PositiveInfinity;
        double maxScore = double.PositiveInfinity;
        //double tempWeight  = 0;
        //double averageTempMetric = 0;
        //  iterate over the vessel parts
        double fixedDeltaTime = TimeWarp.fixedDeltaTime;
        int partCnt = vessel.parts.Count;
        for (int iPart = 0; iPart < partCnt; ++iPart)
        {
            Part p = vessel.parts[iPart];
            if (p == null) 
                continue;
            lastPartIsEngine = false;
            // visit modules
            int moduleCnt = p.Modules.Count;
            for (int jModule = 0; jModule < moduleCnt; ++jModule)
            {
                PartModule m = p.Modules[jModule];
                if (m == null) 
                    continue;
                // do things with modules
                VisitModule(data, m, p, vessel);
            }
            hasEngine |= lastPartIsEngine;
            // do things with parts
            if (p.temperature != 0f && needsTemp) // small gear box has p.temperature==0 - always! Bug? Who knows. Anyway i want to ignore it.
            {
                //double score = p.maxTemp/(Math.Max(0, 1.0 - p.temperature / p.maxTemp) + 1.0e-6) * (1.0 + Math.Max(0.0, p.thermalRadiationFlux + p.thermalConvectionFlux + p.thermalConductionFlux)*p.thermalMassReciprocal*fixedDeltaTime/p.maxTemp);
                //averageTempMetric += score * Math.Max(0, p.maxTemp - p.temperature);
                double t = p.temperature;
                double tskin = p.skinTemperature;
                double tMax = p.maxTemp > 0 ? p.maxTemp  :  2000.0;
                double tskinMax = p.skinMaxTemp > 0 ? p.skinMaxTemp : 2000.0;
                double score = (tMax - t)/tMax;
                double scoreSkin = (tskinMax - tskin)/tskinMax;
                //tempWeight += score;
                if (score < maxScore)
                {
                    maxScore = score;
                    data.highestTemp = t;
                    data.highestTempMax = tMax;
                    data.tempWarnMetric =  tMax - t;
                    data.highestTempIsSkinTemp = false;
                }
                if (scoreSkin < maxScore)
                {
                    maxScore = scoreSkin;
                    data.highestTemp = tskin;
                    data.highestTempMax = tskinMax;
                    data.tempWarnMetric =  tskinMax - tskin;
                    data.highestTempIsSkinTemp = true;
                }
                //DMDebug.Log(string.Format("{0} tmax {1}, t {2}, diff {3}", p.name, p.maxTemp.ToString(), p.temperature.ToString(), (p.maxTemp - p.temperature).ToString()));
            }
        }

        // air
        //DMDebug.Log(string.Format("air avail: {0}, demand {1}", airAvailable, airDemand));
        data.airAvailability = airAvailable / airDemand;
        // data.hasAirAvailability = data.isInAtmosphere && !hasAJE;
        data.hasAirAvailability = false;
        // engine
        data.throttle = vessel.ctrlState.mainThrottle;
        data.hasEnginePerf = hasEngine;
        
        // temperature
        data.tempWarnMetric = data.tempWarnMetric / data.highestTempMax;
        data.hasTemp = needsTemp && data.tempWarnMetric < tempWarnThreshold1;
        //DMDebug.Log(string.Format("tempWarnMetric = {0}, hasTemp = {1}", data.tempWarnMetric.ToString("F3"), data.hasTemp.ToString()));

        if (hasFAR)
            DataFAR.GetFARData(data, vessel);
        else
        {
            data.hasAerodynamics = true;
            data.machNumber = vessel.mach;
            data.q = vessel.dynamicPressurekPa * 1.0e3; // convert to Pa
        }

        UpdateWarningIndicators(data);
    }


    static public void FixedUpdate(Data data, bool computeDerivsAllowed)
    {
        Vessel vessel = FlightGlobals.ActiveVessel;
        double radarAltitude = vessel.altitude - Math.Max(0, vessel.terrainAltitude); // terrainAltitude is the deviation of the terrain from the sea level.
        if (computeDerivsAllowed)
            data.radarAltitudeDeriv = (radarAltitude - data.radarAltitude) / TimeWarp.fixedDeltaTime;
        else
            data.radarAltitudeDeriv = 0;
        data.radarAltitude = radarAltitude;
        data.isDisplayingRadarAlt = data.radarAltitude < 5000.0;
        //DMDebug.Log(string.Format("vertical speed = {0} (?)", data.verticalSpeed));
    }
};


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

public struct KfiTextStyle
{
    public Color color;
    public FontStyle fontStyle;
    public static KfiTextStyle plainWhite
    {
        get { 
            KfiTextStyle s;
            s.color =  Color.white;
            s.fontStyle = FontStyle.Normal;
            return s;
        }
    }
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

/* On how to create GUI by script: http://answers.unity3d.com/questions/849176/how-to-create-a-canvas-and-text-ui-46-object-using.html */
public class KFDText : MonoBehaviour
{
    UnityEngine.UI.Text    gt1_; 
    int        styleId_ = -1;
    Func<Data, KFDContent> getContent_;
    Func<Data, bool> hasChanged_;

    public static KFDText Create(string id, int styleId, Func<Data, KFDContent> getContent, Func<Data, bool> hasChanged)
    {
        // foreground text
        GameObject textGO = new GameObject("KFD-" + id);
        textGO.layer = 12; // navball layer
        KFDText kfi = textGO.AddComponent<KFDText>();
        kfi.gt1_ = textGO.AddComponent<UnityEngine.UI.Text>();
        var shadow = textGO.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(1.0f, -2.0f);
        var shadow2 = textGO.AddComponent<UnityEngine.UI.Shadow>();
        shadow2.effectColor = Color.black;
        shadow2.effectDistance = new Vector2(0.5f, -1.0f);

        kfi.ForceUpdateStyles(styleId);

        kfi.getContent_ = getContent;
        kfi.hasChanged_ = hasChanged;

        return kfi;
    }
        
    private void ForceUpdateStyles(int styleId)
    {
        this.styleId_ = styleId;
        KfiTextStyle s = KFDGuiController.instance.styles[styleId];
        this.gt1_.fontStyle = s.fontStyle;
        this.gt1_.fontSize  = KFDGuiController.instance.fontSize;
        this.gt1_.font      = KFDGuiController.instance.font;
        this.gt1_.color = s.color;
    }

    public void UpdateText(Data data)
    {
        if (hasChanged_(data))
        {
            //DMDebug.Log2(name + " has changed");
            KFDContent c = getContent_(data);

            if (this.styleId_ != c.styleId)  // careful because of potentially costly update
            {
                ForceUpdateStyles(c.styleId);
            }
            // Not going to compare here, since probably the text has actually changed.
            this.gt1_.text = c.text;
        }
        //else
            //DMDebug.Log2(name + " unchanged");
    }


    public void OnDestroy()
    {
        //DMDebug.Log2(this.name + " OnDestroy");
        // release links to make it easier for the gc
        gt1_ = null;
        hasChanged_ = null;
        getContent_ = null;
    }

    public int fontSize
    {
        set
        {
            this.gt1_.fontSize = value;
        }
    }

    public bool enableGameObject
    {
        set 
        { 
            if (this.gameObject.activeSelf != value) 
            {
                //DMDebug.Log2(this.name + " enabled=" + value);
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
    * Since Unity 5 we can use the new UI systems which provides automatic layouting controller such as
    * VerticalLayoutGroup. The latter takes care of the arrangement of texts.
    * */
public class KFDArea : MonoBehaviour
{
    public List<KFDText> items;

    public static KFDArea Create(string id, Vector2 position_, TextAlignment alignment_, Transform parent) // factory, creates game object with attached FKIArea
    {
        GameObject go = new GameObject("KFD-AREA-"+id);
        KFDArea kfi = go.AddComponent<KFDArea>();
        var layout = go.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        var fitter = go.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var recttrafo = go.GetComponent<UnityEngine.RectTransform>();
        if (alignment_ == TextAlignment.Left)
        {
            recttrafo.pivot = new Vector2(0, 0);
            layout.childAlignment = TextAnchor.LowerLeft;
        }
        else if (alignment_ == TextAlignment.Right)
        {
            recttrafo.pivot = new Vector2(1, 0);
            layout.childAlignment = TextAnchor.LowerRight;
        }
        kfi.items = new List<KFDText>();
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position_;
        return kfi;
    }

    void OnDestroy()
    {
        //DMDebug.Log2(this.name + " OnDestroy");
        items.Clear();
    }

    public void Add(KFDText t)
    {
        t.gameObject.transform.SetParent(this.gameObject.transform, false);
        items.Add(t);
    }

    public VerticalAlignment verticalAlignment
    {
        set 
        {
            var rt = this.gameObject.GetComponent<UnityEngine.RectTransform>();
            var layout = this.gameObject.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            var pivot = rt.pivot;
            if (value == VerticalAlignment.Bottom)
            {
                pivot.y = 0;
            }
            else
            {
                pivot.y = 1;
            }
            rt.pivot = pivot;
        }
    }

    public bool enableGameObject
    {
        set 
        { 
            if (this.gameObject.activeSelf != value) 
            {
                //DMDebug.Log2(this.name + " enabled=" + value);
                this.gameObject.SetActive(value); 
            }
        }
        get { return this.gameObject.activeSelf; }
    }
};




/* this class contains information for styling and positioning of the texts */
public class KFDGuiController
{
    static KFDGuiController instance_ = new KFDGuiController(); // allocate when the code loads

    GameObject goAnchor = null;
    GameObject goNavball = null;
    GameObject goAutopilotModes = null;
    GameObject goDVGauge = null;
    GameObject goNavballIVACollapse = null;

    int timeSecondsPerDay;
    int timeSecondsPerYear;

    float baseFontSizeIVA = 16; // font size @ 100% UI scale setting
    float baseFontSizeExternal = 16;
    float topAnchorOffsetX = 0.0f;

    public int   fontSize;
    public float screenAnchorRight;
    public float screenAnchorLeft;
    public float screenAnchorVertical;
    public bool  ready = false;
    public bool  isIVA = false;
    public bool  isMapMode = false;

    public UnityEngine.Font font = null;
    public KfiTextStyle[] styles = null;

    // gui stuff
    KFDText[] texts = null;
    int[] markers = null;
    int markerMaster = 0;
    KFDArea leftArea, rightArea;
    enum TxtIdx
    {
        MACH = 0, AIR, ALT, STALL, Q, TEMP, TNODE, AP, PE, ENGINEPERF, VSPEED, COUNT
    };


    public void LoadSettings(ConfigNode settings)
    {
        Util.TryReadValue(ref baseFontSizeIVA, settings, "baseFontSizeIVA");
        Util.TryReadValue(ref baseFontSizeExternal, settings, "baseFontSizeExternal");
        Util.TryReadValue(ref topAnchorOffsetX, settings, "topAnchorOffsetX");
    }

    public void SaveSettings(ConfigNode settings)
    {
        settings.AddValue("baseFontSizeIVA", baseFontSizeIVA);
        settings.AddValue("baseFontSizeExternal", baseFontSizeExternal);
        settings.AddValue("topAnchorOffsetX", topAnchorOffsetX);
    }

    public void Init()
    {
        try
        {
            var uimaster = KSP.UI.UIMasterController.Instance;
            GameObject goMaster = uimaster.gameObject;
            /* Note: The origin of the UI coordinate frame is in the center of the screen, with positive x to the right and positive y up.
                * Positions are given in units of pixels. */
            goNavball = goMaster.GetChild("NavBall_OverlayMask");
            goNavballIVACollapse = goMaster.GetChild("NavballFrame").GetChild("IVAEVACollapseGroup");
            goAutopilotModes = goMaster.GetChild("AutopilotModes");
            goDVGauge = goMaster.GetChild("deltaVreadout");
            goAnchor = goMaster.GetChild("UIModeFrame"); // The origin of its local frame is in the bottom left corner of the screen.
        }
        catch (NullReferenceException)
        {
            // Suppress silently. The following check will deal with missing refernces.
        }
        if (goNavball == null || goNavballIVACollapse == null || goAutopilotModes == null || goDVGauge == null || goAnchor == null)
        {
            DMDebug.Log("Not (yet) able to obtain all references to various gui elements");
            ready = false;
            return;
        }

        if (GameSettings.KERBIN_TIME)
        {
            CelestialBody b = FlightGlobals.GetHomeBody(); // should point to kerbin
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

        styles = new KfiTextStyle[5];
        var s = KfiTextStyle.plainWhite;
        styles[MyStyleId.Plain] = s;
        styles[MyStyleId.Emph] = s;
        s.color = XKCDColors.Grey;
        styles[MyStyleId.Greyed] = s;
        s.color = Color.yellow;
        styles[MyStyleId.Warn1] = s;
        s.color = Color.red;
        styles[MyStyleId.Warn2] = s;
        styles[MyStyleId.Emph].fontStyle = styles[MyStyleId.Warn1].fontStyle = styles[MyStyleId.Warn2].fontStyle = FontStyle.Bold;

        // Arial.ttf is the default font of Unity 3d which should be always available
        // on any system. Still have to figure out how to obtain KSP fonts.
        // See http://docs.unity3d.com/Manual/class-Font.html
        font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");

        ready = true;

        SetupGUI();
    }

    public void Destroy()
    {
        // fun fact: all my MonoBehaviour derivatives are destroyed automagically
        texts = null; // texts are actually destroyed by their parents
        markers = null;
        if (leftArea) GameObject.Destroy(leftArea.gameObject); // better be safe.
        if (rightArea) GameObject.Destroy(rightArea.gameObject);
        leftArea = null;
        rightArea = null;

        // It's safer to remove all references and not have "dead" objects around. Even more so because the instance is always accessible .
        goNavball = null;
        goAutopilotModes = null;
        goDVGauge = null;
        goAnchor = null;
        ready = false;
    }

    private void UpdateGuiConfigurationData()
    {
        float uiScale = KSP.UI.UIMasterController.Instance.uiScale;
        isIVA = isMapMode = false;
        switch (CameraManager.Instance.currentCameraMode)
        {
            case CameraManager.CameraMode.Internal:
                goto case CameraManager.CameraMode.IVA;
            case CameraManager.CameraMode.IVA: 
                isIVA = true;
                break;
            case CameraManager.CameraMode.Map:
                isMapMode = true;
                break;
        }
        if (isIVA)
        {
            fontSize = Mathf.RoundToInt(baseFontSizeIVA * uiScale);
            var x = Screen.width / uiScale * 0.5f;
            screenAnchorLeft  = x - baseFontSizeIVA;
            screenAnchorRight = x + baseFontSizeIVA;
            screenAnchorVertical = Screen.height / uiScale - 10f;
        }
        else
        {
            fontSize = Mathf.RoundToInt(baseFontSizeExternal * uiScale);
            var anchorTrafo = anchorObject.transform;
            var goAnchorLeft = goNavball;
            var goAnchorRight = goNavball;
            var goAnchorVertical = goNavballIVACollapse;
            if (goAutopilotModes.GetComponent<Canvas>().isActiveAndEnabled)
            {
                goAnchorLeft = goAutopilotModes;
            }
            if (goDVGauge.activeInHierarchy)
            {
                goAnchorRight = goDVGauge;
            }
            var pLeft = Util.TransformPoint(goAnchorLeft.transform, anchorObject.transform, goAnchorLeft.GetComponent<RectTransform>().rect.min);
            var pRight = Util.TransformPoint(goAnchorRight.transform, anchorObject.transform, goAnchorRight.GetComponent<RectTransform>().rect.max);
            const float ANCHOR_POSITION_COMPENSATION = 11f;
            const float VERTICAL_TEXT_OFFSET = 5f;
            var pVert = Util.TransformPoint(goAnchorVertical.transform, anchorObject.transform, 0f, ANCHOR_POSITION_COMPENSATION+VERTICAL_TEXT_OFFSET);
            screenAnchorLeft = pLeft.x;
            screenAnchorRight = pRight.x + 5f;
            screenAnchorVertical = pVert.y;
        }
    }

    public void ConfigureGuiElements()
    {
        float lastFontSize = this.fontSize;
        bool  lastIsIVA    = this.isIVA;
        
        UpdateGuiConfigurationData();

        // attach areas to the navball basically
        leftArea.transform.localPosition = new Vector3(screenAnchorLeft, screenAnchorVertical);
        rightArea.transform.localPosition = new Vector3(screenAnchorRight, screenAnchorVertical);
        
        if (lastIsIVA != this.isIVA)
        {
            leftArea.verticalAlignment = isIVA ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            rightArea.verticalAlignment = isIVA ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        }

        if (lastFontSize != this.fontSize)
        {
            foreach (var t in texts)
                t.fontSize = fontSize;
        }
    }

    public void UpdateValues(Data data)
    {
        ++markerMaster;
        if (data.isInAtmosphere && data.hasEnginePerf)
        {
            markers[(int)TxtIdx.ENGINEPERF] = markerMaster;
        }
        if (data.isInAtmosphere && !data.isLanded)
        {
            if (data.hasAerodynamics)
            {
                markers[(int)TxtIdx.MACH] = markerMaster;
                markers[(int)TxtIdx.Q] = markerMaster;
            }
            if (data.hasAirAvailability)
            {
                markers[(int)TxtIdx.AIR] = markerMaster;
            }
            if (data.hasStalls)
            {
                markers[(int)TxtIdx.STALL] = markerMaster;
            }
            if (data.hasTemp)
            {
                markers[(int)TxtIdx.TEMP] = markerMaster;
            }
        }

        if (!data.isLanded)
        {
            if (KFDGuiController.instance.isMapMode || data.isDisplayingRadarAlt)
            {
                markers[(int)TxtIdx.ALT] = markerMaster;
            }
            if (KFDGuiController.instance.isMapMode)
            {
                markers[(int)TxtIdx.VSPEED] = markerMaster;
            }
            if (data.isAtmosphericLowLevelFlight == false)
            {
                markers[(int)TxtIdx.TNODE] = markers[(int)TxtIdx.AP] = markers[(int)TxtIdx.PE] = markerMaster;
            }
        }

        // disable unmarked texts, update marked ones
        for (int i = 0; i < texts.Length; ++i)
        {
            texts[i].enableGameObject = (markers[i] == markerMaster);
            if (markers[i] == markerMaster)
                texts[i].UpdateText(data);
        }
    }

    public void ToggleDisplay(bool on)
    {   
        // need to check if the areas are still there because on destruction of the scene they might be already gone without knowing
        if (leftArea) leftArea.enableGameObject = on;
        if (rightArea) rightArea.enableGameObject = on;                
    }

    public static KFDGuiController instance
    {
        get { return instance_; }
    }

    private GameObject anchorObject
    {
        get { return goAnchor; }
    }

    private static bool SetIf<T1>(ref T1 dst, T1 src, bool b)
    {
        if (b) { dst = src; return true; }
        else return false;
    }

    private static bool SetIf<T1, T2>(ref T1 dst1, ref T2 dst2, T1 src1, T2 src2, bool b)
    {
        if (b) { 
            dst1 = src1; 
            dst2 = src2;
            return true; 
        }
        else return false;
    }

    private void SetupGUI()
    {
        DMDebug.Log2("SetupGUI");
        leftArea = KFDArea.Create("left", new Vector2(KFDGuiController.instance.screenAnchorLeft, 0f), TextAlignment.Right, null);
        rightArea = KFDArea.Create("right", new Vector2(KFDGuiController.instance.screenAnchorRight, 0f), TextAlignment.Left, null);
        leftArea.transform.SetParent(anchorObject.transform, false);
        rightArea.transform.SetParent(anchorObject.transform, false);

        texts = new KFDText[(int)TxtIdx.COUNT];

        double tmpMach = -1;
        texts[(int)TxtIdx.MACH] = KFDText.Create("mach", MyStyleId.Emph,
            (Data d) => new KFDContent("Mach " + d.machNumber.ToString("F2"), MyStyleId.Emph),
            (Data d) => SetIf(ref tmpMach, d.machNumber, !Util.AlmostEqual(d.machNumber, tmpMach, 0.01)));

        double tmpAir = -1;
        texts[(int)TxtIdx.AIR] = KFDText.Create("air", MyStyleId.Greyed,
            (Data d) => new KFDContent("Intake" + (d.airAvailability < 2 ? "  " + (d.airAvailability * 100d).ToString("F0") + "%" : ""), d.warnAir),
            (Data d) => SetIf(ref tmpAir, d.airAvailability, (d.airAvailability < 2.1 || tmpAir<2.1) ? !Util.AlmostEqual(d.airAvailability, tmpAir, 0.01) : false));

        Func<Data, KFDContent> fmtEnginePerf = (Data d) =>
        {
            string txt;
            if (d.hasAirBreathingEngines)
                txt = String.Format("J {0} kN |{1}%|", d.airBreatherThrust.ToString("F0"), (100*d.throttle).ToString("F0"));
            else
                txt = String.Format("R {0} kN |{1}%|", d.totalThrust.ToString("F0"), (100*d.throttle).ToString("F0"));
            return new KFDContent(txt, MyStyleId.Greyed);
        };
        double tmpEngPerf = -1;
        double tmpThrottle = -1;
        bool tmpUseAirBreathers = false;
        Func<Data, bool> hasChangedEnginePerf = (Data d) =>
        {
            bool b1 = Util.AlmostEqual(d.totalThrust, tmpEngPerf, 0.5);
            bool b2 = b1 && Util.AlmostEqual(d.throttle, tmpThrottle, 0.005);
            bool b3 = b2 && tmpUseAirBreathers == d.hasAirBreathingEngines;
            return !b3;
        };
        texts[(int)TxtIdx.ENGINEPERF] = KFDText.Create("eng", MyStyleId.Greyed,
            fmtEnginePerf, 
            hasChangedEnginePerf);

        int tmpStall = -1;
        texts[(int)TxtIdx.STALL] = KFDText.Create("stall", MyStyleId.Greyed,
            (Data d) => new KFDContent("Stall", d.warnStall),
            (Data d) => SetIf(ref tmpStall, d.warnStall, d.warnStall != tmpStall));

        double tmpQ = -1;
        texts[(int)TxtIdx.Q] = KFDText.Create("q", MyStyleId.Greyed,
            (Data d) => new KFDContent("Q  " + KFDGuiController.FormatPressure(d.q), d.warnQ),
            (Data d) => SetIf(ref tmpQ, d.q, !Util.AlmostEqualRel(d.q, tmpQ, 0.01)));

        double tmpTemp = -1;
        int    tmpWarnTemp = -1;
        Func<Data, KFDContent> fmtTemperature = (Data d) =>
        {
            string isskin = d.highestTempIsSkinTemp ? "(I)" : "";
            string s = string.Format("T {0} / {1}{2} K", d.highestTemp.ToString("F0"), d.highestTempMax.ToString("F0"), isskin);
            return new KFDContent(s, d.warnTemp);
        };
        texts[(int)TxtIdx.TEMP] = KFDText.Create("temp", MyStyleId.Greyed,
            fmtTemperature,
            (Data d) => SetIf(ref tmpTemp, ref tmpWarnTemp, d.highestTemp, d.warnTemp, !Util.AlmostEqual(d.highestTemp, tmpTemp, 1) || d.warnTemp != tmpWarnTemp));

        Func<Data, KFDContent> fmtAltitude = (Data d) =>
        {
            string label;
            if (d.isDisplayingRadarAlt)
                label = string.Format("Alt {0} R", KFDGuiController.FormatRadarAltitude(d.radarAltitude));
            else 
                label = "Alt "+KFDGuiController.FormatAltitude(d.altitude);
            int warnlevel = MyStyleId.Emph;
            if (d.timeToImpact < 5)
                warnlevel = MyStyleId.Warn2;
            else if (d.timeToImpact < 10 || d.radarAltitude < 200.0)
                warnlevel = MyStyleId.Warn1;
            return new KFDContent(label, warnlevel);
        };
        double tmRadarAltitudeDeriv = double.PositiveInfinity;
        double tmpAlt = -1;
        texts[(int)TxtIdx.ALT] = KFDText.Create("alt", MyStyleId.Emph,
            fmtAltitude,
            (Data d) => SetIf(ref tmpAlt, ref tmRadarAltitudeDeriv, d.altitude, d.radarAltitudeDeriv, !Util.AlmostEqualRel(d.altitude, tmpAlt, 0.001) || !Util.AlmostEqualRel(d.radarAltitudeDeriv, tmRadarAltitudeDeriv, 0.01)));

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
            timeLabel = "T" + timeLabel + " -" + KFDGuiController.FormatTime(d.timeToNode);
            return new KFDContent(timeLabel, MyStyleId.Plain);
        };

        double tmpTNode = -1;
        texts[(int)TxtIdx.TNODE] = KFDText.Create("tnode", MyStyleId.Plain,
            fmtTime,
            (Data d) => SetIf(ref tmpTNode, d.timeToNode, !Util.AlmostEqual(d.timeToNode, tmpTNode, 1)));

        Data.NextNode tmpNextNode1 = Data.NextNode.Maneuver;
        double tmpAp = -1;
        texts[(int)TxtIdx.AP] = KFDText.Create("ap", MyStyleId.Plain,
            (Data d) => new KFDContent("Ap " + KFDGuiController.FormatAltitude(d.apoapsis), d.nextNode == Data.NextNode.Ap ? MyStyleId.Emph : MyStyleId.Plain),
            (Data d) => SetIf(ref tmpAp, ref tmpNextNode1, d.apoapsis, d.nextNode, !Util.AlmostEqualRel(d.apoapsis, tmpAp, 0.001) || d.nextNode != tmpNextNode1));

        Data.NextNode tmpNextNode2 = Data.NextNode.Maneuver;
        double tmpPe = 0;
        texts[(int)TxtIdx.PE] = KFDText.Create("pe", MyStyleId.Plain,
            (Data d) => new KFDContent("Pe " + KFDGuiController.FormatAltitude(d.periapsis), d.nextNode == Data.NextNode.Pe ? MyStyleId.Emph : MyStyleId.Plain),
            (Data d) => SetIf(ref tmpPe, ref tmpNextNode2, d.periapsis, d.nextNode, !Util.AlmostEqualRel(d.periapsis, tmpPe, 0.001) || d.nextNode != tmpNextNode2));

        double tmpVertSpeed = -1;
        texts[(int)TxtIdx.VSPEED] = KFDText.Create("vertspeed", MyStyleId.Emph,
            (Data d) => new KFDContent("VS " + KFDGuiController.FormatVerticalSpeed(d.verticalSpeed), KFDGuiController.StyleVerticalSpeed(d)),
            (Data d) => SetIf(ref tmpVertSpeed, d.verticalSpeed, !Util.AlmostEqualRel(d.verticalSpeed, tmpVertSpeed, 0.001)));

        leftArea.Add(texts[(int)TxtIdx.VSPEED]);
        leftArea.Add(texts[(int)TxtIdx.ALT]);
        leftArea.Add(texts[(int)TxtIdx.TNODE]);
        leftArea.Add(texts[(int)TxtIdx.AP]);
        leftArea.Add(texts[(int)TxtIdx.PE]);
        rightArea.Add(texts[(int)TxtIdx.MACH]);
        rightArea.Add(texts[(int)TxtIdx.AIR]);
        rightArea.Add(texts[(int)TxtIdx.ENGINEPERF]);
        rightArea.Add(texts[(int)TxtIdx.STALL]);
        rightArea.Add(texts[(int)TxtIdx.Q]);
        rightArea.Add(texts[(int)TxtIdx.TEMP]);

        markers = new int[(int)TxtIdx.COUNT];
    }


    #region GUIUtil
    // contribution by  Scialytic
    public static String FormatVerticalSpeed(double x)
    {
        double a = Math.Abs(x);
        if (a >= 1.0e3)
        {
            x *= 1.0e-3;
            a *= 1.0e-3;
            return x.ToString(a < 10 ? "F2" : (a < 100 ? "F1" : "F0")) + " km/s";
        }
        else
        {
            return x.ToString("F0") + " m/s";
        }
    }

    public static int StyleVerticalSpeed(Data d)
    {
        //return MyStyleId.Plain;
        if ((d.verticalSpeed<-2.0 && d.radarAltitude<50.0) || (d.radarAltitude < -10.0*d.verticalSpeed))
        { 
            if (d.radarAltitude < -5.0 * d.verticalSpeed)
                return MyStyleId.Warn2;
            else
                return MyStyleId.Warn1;
        }
        else
            return MyStyleId.Emph;
    }

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
public class DMFlightData : DaMichelToolbarSuperWrapper.PluginWithToolbarSupport
{
    double dtSinceLastUpdate;

    Data data = new Data();
    bool isRecordingInFixedUpdate = false, lastIsRecordingInFixedUpdate = false;

    protected override DaMichelToolbarSuperWrapper.ToolbarInfo GetToolbarInfo()
    {
        return new DaMichelToolbarSuperWrapper.ToolbarInfo {
            name = "KerbalFlightData",
            tooltip = "KerbalFlightData On/Off Switch",
            toolbarTexture = "KerbalFlightData/toolbarbutton",
            launcherTexture = "KerbalFlightData/icon",
            visibleInScenes = new GameScenes[] { GameScenes.FLIGHT }
        };
    }

    void Awake() // Awake is called when the script instance is being loaded.
    {
        DMDebug.Log2(name + " awake!");
        LoadSettings();

        DataSources.Init();

        InitializeToolbars();

        GameEvents.OnGameSettingsApplied.Add(OnSettingsApplied);

        OnGuiVisibilityChange(); // might start with it disabled
    }


    void OnDestroy() // This function is called when the MonoBehaviour will be destroyed.
    {
        DMDebug.Log2(name + " destroyed!");
        SaveSettings();

        KFDGuiController.instance.Destroy();

        // unregister, or else errors occur
        GameEvents.OnGameSettingsApplied.Remove(OnSettingsApplied);
        
        TearDownToolbars();
    }


    void OnSettingsApplied()
    {
        KFDGuiController guiInfo = KFDGuiController.instance;
        if (guiInfo.ready)
        {
            guiInfo.ConfigureGuiElements();
        }
    }

    protected override  void OnGuiVisibilityChange()
    {
        enabled = isGuiVisible;
    }


    void SaveSettings()
    {
        ConfigNode settings = new ConfigNode();
        settings.name = "SETTINGS";
        SaveMutableToolbarSettings(settings);
        SaveImmutableToolbarSettings(settings);
        KFDGuiController.instance.SaveSettings(settings);
        settings.Save(AssemblyLoader.loadedAssemblies.GetPathByType(typeof(DMFlightData)) + "/settings.cfg");
    }


    void LoadSettings()
    {
        ConfigNode settings = ConfigNode.Load(AssemblyLoader.loadedAssemblies.GetPathByType(typeof(DMFlightData)) + "/settings.cfg");
        if (settings != null)
        {
            LoadMutableToolbarSettings(settings);
            LoadImmutableToolbarSettings(settings);
            KFDGuiController.instance.LoadSettings(settings);
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
        KFDGuiController.instance.ToggleDisplay(false); // update won't be called so lets disable the display here
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

    void FixedUpdate()
    {
        if (isRecordingInFixedUpdate) 
            DataSources.FixedUpdate(data, lastIsRecordingInFixedUpdate);
        lastIsRecordingInFixedUpdate = isRecordingInFixedUpdate;
    }

    void LateUpdate()
    {
        #if DEBUG
        if (Input.GetKeyDown(KeyCode.O))
        {
            DebugOutput();
        }
        #endif

        dtSinceLastUpdate += Time.unscaledDeltaTime;

        if (!KFDGuiController.instance.ready)
            KFDGuiController.instance.Init();
        if (!KFDGuiController.instance.ready)
            return;

        bool on = CheckShouldDisplayBeOn();
        
        KFDGuiController.instance.ToggleDisplay(on);
        if (on)
        {
            isRecordingInFixedUpdate = true;
            // obtain data
            Vessel vessel = FlightGlobals.ActiveVessel;
            DataSources.FillDataInstance(data, vessel);
            KFDGuiController.instance.ConfigureGuiElements();
        }
        else
        {
            isRecordingInFixedUpdate = false;
        }

        if (on && CheckFullUpdateTimer())
        {
            KFDGuiController.instance.UpdateValues(data);
        }
    }

    #if DEBUG
    static void DebugOutput()
    {
        Debug.Log("KerbalFlightData DebugOutput");
        DMDebug dbg = new DMDebug();
        var obj = FlightUIModeController.Instance.gameObject;
        obj = DMDebug.GetRoot(obj);
        dbg.PrintGameObjectHierarchy(obj, 0);
        dbg.Out("---------------------------------------------------------", 0);
        var fonts = FindObjectsOfType(typeof(Font)) as Font[];
        foreach (Font font in fonts)
            dbg.Out(font.name, 1);
        dbg.Out("---------------------------------------------------------", 0);
        obj = GameObject.Find("KFD-tnode");
        dbg.PrintHierarchy(obj, 0, false);
        dbg.Out("---------------------------------------------------------", 0);
        obj = GameObject.Find("TextSpeed");
        dbg.PrintHierarchy(obj, 0, false);
        dbg.Out("---------------------------------------------------------", 0);
        obj = GameObject.Find("AutopilotModes");
        dbg.PrintHierarchy(obj, 0, false);
        dbg.Out("---------------------------------------------------------", 0);
        obj = GameObject.Find("NavballFrame");
        dbg.PrintHierarchy(obj, 0, false);
        dbg.Out("---------------------------------------------------------", 0);
        obj = obj.GetChild("IVAEVACollapseGroup");
        dbg.PrintHierarchy(obj, 0, false);
        dbg.Out("---------------------------------------------------------", 0);
        var vessel = FlightGlobals.ActiveVessel;
        dbg.Out("----- vessel : ----", 0);
        dbg.PrintHierarchy(vessel, 0, false);
        var f = KSP.IO.TextWriter.CreateForType<DMFlightData>("GameObject.txt", null);
        f.Write(dbg.ToString());
        f.Close();
    }
    #endif
}


} // namespace
