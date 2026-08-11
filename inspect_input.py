import clr
import System.Reflection

path = r"D:\SteamLibrary\steamapps\common\Nuclear Option\NuclearOption_Data\Managed\Assembly-CSharp.dll"
asm = System.Reflection.Assembly.LoadFrom(path)

try:
    all_types = asm.GetTypes()
except System.Reflection.ReflectionTypeLoadException as ex:
    all_types = [t for t in ex.Types if t is not None]

# Search for Input-related types that might handle scroll
print("=== Types with 'Input' in name that have scroll/zoom/mouse members ===")
for t in all_types:
    if "input" not in t.Name.lower():
        continue
    hits = []
    for f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static):
        fname = f.Name.lower()
        if any(k in fname for k in ["scroll", "zoom", "mouse", "wheel", "fov"]):
            hits.append("field:{} ({})".format(f.Name, f.FieldType.Name))
    for m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static):
        if m.DeclaringType != t:
            continue
        mname = m.Name.lower()
        if any(k in mname for k in ["scroll", "zoom", "mouse", "wheel", "fov"]):
            hits.append("method:{}".format(m.Name))
    if hits:
        print("\n{}:".format(t.Name))
        for h in hits:
            print("  {}".format(h))

# Also look at CameraRelativeState
print("\n=== CameraRelativeState fields ===")
for t in all_types:
    if t.Name == "CameraRelativeState":
        for f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance):
            print("  {} {}".format(f.FieldType.Name, f.Name))

# Check CameraStateManager for methods that might reference allowInputs
print("\n=== CameraStateManager methods ===")
for t in all_types:
    if t.Name == "CameraStateManager":
        for m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance):
            if m.DeclaringType == t:
                print("  {}".format(m.Name))

# Search ALL types for members named exactly or containing 'allowInputs'
print("\n=== Types with 'allowInputs' member ===")
for t in all_types:
    for f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static):
        if f.Name.lower() == "allowinputs":
            print("  {}.{} ({})".format(t.Name, f.Name, f.FieldType.Name))
