import clr
import System.Reflection

path = r"D:\SteamLibrary\steamapps\common\Nuclear Option\NuclearOption_Data\Managed\Assembly-CSharp.dll"
asm = System.Reflection.Assembly.LoadFrom(path)

try:
    types = [t.Name for t in asm.GetTypes()]
except System.Reflection.ReflectionTypeLoadException as ex:
    types = [t.Name for t in ex.Types if t is not None]

keywords = ["zoom", "camera", "scroll", "mouse", "view", "fov"]
matches = sorted([t for t in types if any(k in t.lower() for k in keywords)])
print("Found {} matching types:".format(len(matches)))
for t in matches:
    print(t)

with open(r"C:\Users\Kyle\Documents\code\no-vor\all_types.txt", "w") as f:
    for t in sorted(types):
        f.write(t + "\n")
