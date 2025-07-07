# BitPlanner Godot Export Templates

Release builds of BitPlanner for Windows and Linux use custom Godot Engine export templates. Export templates are relatively big binary files, so they are not published here, but with provided files you can replicate them.

1. Check out the Godot Engine's [Building From Source documentation](https://docs.godotengine.org/en/stable/contributing/development/compiling/index.html), you may need to go through various steps to prepare your environment for building;

2. Clone Godot repository: `git clone https://github.com/godotengine/godot.git --depth 1 -b 4.4`;

3. Copy `custom.py` and (optionally, if you're on Linux) `build_bitplanner_templates.sh` to the root of your cloned Godot repository;

4. On Linux, you can run `build_bitplanner_templates.sh` from Godot repository directory to compile export templates. On other host systems, look at commands to execute in the script;

5. Compiled templates will appear in `bin` subdirectory of your cloned Godot repository. Copy all files from there here. BitPlanner's Godot project is already configured to use templates from this directory.
