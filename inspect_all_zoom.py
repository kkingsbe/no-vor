import clr
import System.Reflection

path = r"D:\SteamLibrary\steamapps\common\Nuclear Option\NuclearOption_Data\Managed\Assembly-CSharp.dll"
asm = System.Reflection.Assembly.LoadFrom(path)

try:
    all_types = asm.GetTypes()
except System.Reflection.ReflectionTypeLoadException as ex:
    all_types = [t for t in ex.Types if t is not None]

# Find all types that have fields/methods/properties containing scroll/zoom/mousewheel
keywords = ["scroll", "zoom", "mousewheel", "mouse_wheel", "fov"]

print("=== Types with scroll/zoom/fov members ===")
for t in all_types:
    hits = []
    for f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static):
        if any(k in f.Name.lower() for k in keywords):
            hits.append("field:{} ({})".format(f.Name, f.FieldType.Name))
    for m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static):
        if m.DeclaringType == t and any(k in m.Name.lower() for k in keywords):
            hits.append("method:{}".format(m.Name))
    for p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static):
        if any(k in p.Name.lower() for k in keywords):
            hits.append("prop:{}".format(p.Name))
    if hits:
        print("\n{}:".format(t.Name))
        for h in hits:
            print("  {}".format(h))

# Also check Rewired types
rewired_path = r"D:\SteamLibrary\steamapps\common\Nuclear Option\NuclearOption_Data\Managed\Rewired_Core.dll"
try:
    rewired_asm = System.Reflection.Assembly.LoadFrom(rewired_path)
    player = [t for t in rewired_asm.GetTypes() if t.Name == "Player"]
    if player:
        print("\n=== Rewired Player methods with 'GetAxis', 'GetButton' ===")
        for m in player[0].GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance):
            if "getaxis" in m.Name.lower() or "getbutton" in m.Name.lower():
                print("  {}".format(m.Name))
except Exception as e:
    print("Could not inspect Rewired: {}".format(e))
