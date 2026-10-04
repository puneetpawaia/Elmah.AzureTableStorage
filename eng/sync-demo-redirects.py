"""Refresh the demo's binding redirects after a dependency update and build."""

from copy import deepcopy
from pathlib import Path
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
DEMO = ROOT / "samples/demo/Elmah.AzureTableStorage.Demo"
ASSEMBLY_NAMESPACE = "urn:schemas-microsoft-com:asm.v1"


def main():
    generated = ET.parse(DEMO / "bin/Elmah.AzureTableStorage.Demo.dll.config").getroot()
    path = DEMO / "Web.config"
    parser = ET.XMLParser(target=ET.TreeBuilder(insert_comments=True))
    tree = ET.parse(path, parser=parser)
    root = tree.getroot()
    runtime = root.find("runtime")
    if runtime is not None:
        root.remove(runtime)
    runtime = ET.SubElement(root, "runtime")
    bindings = ET.SubElement(runtime, "{" + ASSEMBLY_NAMESPACE + "}assemblyBinding")
    for node in generated.findall(".//{" + ASSEMBLY_NAMESPACE + "}dependentAssembly"):
        bindings.append(deepcopy(node))
    for builder in root.findall("./configBuilders/builders/add"):
        builder.set("type", builder.attrib["type"].replace("Version=2.0.0.0", "Version=3.0.0.0"))
    ET.register_namespace("asm", ASSEMBLY_NAMESPACE)
    ET.indent(tree, space="  ")
    tree.write(path, encoding="utf-8", xml_declaration=True)
    path.write_text("\n".join(line.rstrip() for line in path.read_text().splitlines()) + "\n", encoding="utf-8")
    print("Updated demo binding redirects from the built dependency graph")


if __name__ == "__main__":
    main()
