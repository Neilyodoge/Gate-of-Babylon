"""Current UI regression entry point: one-click bake workflow."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name("test_oneclick_max.py")), run_name="__main__")
