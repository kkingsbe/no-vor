import clr
import System.Reflection

path = r"D:\SteamLibrary\steamapps\common\Nuclear Option\NuclearOption_Data\Managed\Assembly-CSharp.dll"
asm = System.Reflection.Assembly.LoadFrom(path)

try:
    all_types = asm.GetTypes()
except System.Reflection.ReflectionTypeLoadException as ex:
    all_types = [t for t in ex.Types if t is not None]

manager = [t for t in all_types if t.Name == "CameraStateManager"][0]
for f in manager.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance):
    if f.Name in ["allowInputs", "enableMouseLook", "desiredFOV", "fovChangeSpeed", "fovChangeInertia"]:
        print("{}: Public={}".format(f.Name, f.IsPublic))
