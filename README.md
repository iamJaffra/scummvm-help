# ScummVM-Help

ScummVM-Help is a helper library which aims to facilitate the creation of ASL scripts for ScummVM games.

Its main goal is to enable people to write ASLs that work across all release versions of ScummVM, and to do so without needing multiple state descriptors and version checks, and without needing to perform tedious pointer scans.

Currently, every release version from `2.0.0` onwards is supported:
- `2.0.0` `32-bit`
- `2.1.0` `32-bit` and `64-bit`
- `2.1.1` `32-bit` and `64-bit`
- `2.1.2` `32-bit` and `64-bit`
- `2.2.0` `32-bit` and `64-bit`
- `2.5.0` `32-bit` and `64-bit`
- `2.5.1` `32-bit` and `64-bit`
- `2.6.0` `32-bit` and `64-bit`
- `2.6.1` `32-bit` and `64-bit`
- `2.7.0` `32-bit` and `64-bit`
- `2.7.1` `32-bit` and `64-bit`
- `2.8.0` `32-bit` and `64-bit`
- `2.8.1` `32-bit` and `64-bit`
- `2.9.0` `32-bit` and `64-bit`
- `2.9.1` `32-bit` and `64-bit`

## Usage

Load the helper in `startup {}` specifying the engine of your game. For example, if the ASL script is for an SCI game:

```c#
startup {
  Assembly.Load(File.ReadAllBytes("Components/scummvm-help")).CreateInstance("SCI");
}
```
This sets the helper to `vars.ScummVM`.

Initialize the helper at the start of `init {}`: 

```c#
init {
  vars.ScummVM.Init();
}
```

This attaches the helper to the game process and starts a signature scan for ScummVM's `g_engine`.

### g_engine

`g_engine` is a pointer to the currently active engine and will function as the base address of any pointer paths you build using the helper.

If you just want `g_engine` and build your own raw pointer paths, you can use `vars.ScummVM.GEngine` as the base address, e.g.:

```c#
  var dp = new DeepPointer((IntPtr)vars.ScummVM.GEngine, 0x50, 0x14);
```

### Watch\<T>, Read\<T>, and offset resolution via field names

The helper provides two methods for reading memory.

`vars.ScummVM.Watch<T>` creates a `MemoryWatcher<T>` using field names.

For example:

```c#
vars.ScummVM["card"] = vars.ScummVM.Watch<ushort>("_card", "_id");
```

You can then access the value via `vars.ScummVM["card"].Current` or, more conveniently, via `current.card`.

`vars.ScummVM.Read<T>` is for one-time memory reads:

```c#
var arraySize = vars.ScummVM.Read<int>("_storage", "_mask");
```

### Updating the watchers

To keep your watchers updated, place this in `update {}`:

```c#
update {
  vars.ScummVM.Update();
}
```

## Example ASLs

- [Torin's Passage](https://raw.githubusercontent.com/iamJaffra/ASLs/refs/heads/main/TorinsPassage.asl)
- [Riven](https://raw.githubusercontent.com/iamJaffra/ASLs/refs/heads/main/Riven.asl)

## Supported engines (work in progress)

- Groovie
- Mohawk_Myst
- Mohawk_Riven
- SCI
