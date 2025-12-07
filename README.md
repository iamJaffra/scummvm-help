# ScummVM-Help

ScummVM-Help is a helper library which aims to facilitate the creation of ASL scripts for ScummVM games.

Its main goal is to enable people to write ASLs that work across all versions of ScummVM (without multiple state descriptors and version checks), and to eliminate the need for tedious pointer scans.

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

Full functionality cannot be guaranteed when playing on custom or nightly builds of ScummVM.

## Usage

Load the helper in `startup {}` specifying the engine of your game. For example, if the ASL script is for an SCI game:

```c#
startup {
  Assembly.Load(File.ReadAllBytes("Components/scummvm-help")).CreateInstance("SCI");
}
```
This loads the helper into `vars.ScummVM` to be accessed anywhere in your script.

Then, initialize the helper at the start of `init {}`: 

```c#
init {
  vars.ScummVM.Init();
}
```

This attaches the helper to the game process and starts a signature scan for ScummVM's `g_engine`.

### The g_engine pointer

`g_engine` is a pointer within ScummVM which points to the currently active engine and which will function as the base address of any pointer paths you build using the helper.

If you just want `g_engine` and build your own raw pointer paths, you can use `vars.ScummVM.GEngine` as the base address, e.g.:

```c#
var dp = new DeepPointer((IntPtr)vars.ScummVM.GEngine, 0x50, 0x14);
```

*(Note: The `g_engine` scan is expected to work even on most custom builds and all nightly builds post 2.0.0)*

### Memory Utilities 

The main advantage the library offers is the creation of pointer paths using ScummVM class field names. 

For this, the API exposes the following methods:

#### Watch\<T>

`vars.ScummVM.Watch<T>` creates a `MemoryWatcher<T>` using field names.

For example:

```c#
vars.myWatcher = vars.ScummVM.Watch<ushort>("_card", "_id");
```

Now, `vars.myWatcher` is a `MemoryWatcher` watching the value at the address reached via `g_engine -> _card -> _id`.

#### Read\<T>

The second memory utility is `Read<T>`. As the name suggests, it merely reads a value of type `T` at the specified address rather than creating a watcher.

Use it for one-time reads, like this:

```c#
var capacity = vars.ScummVM.Read<int>("_gamestate", "_segMan", "_heap", "_capacity");
```

### Using vars.ScummVM\[\<NAME>]

You can also assign any `MemoryWatcher`s to `vars.ScummVM[<NAME>]` like this:

```c#
vars.ScummVM["card"] = vars.ScummVM.Watch<ushort>("_card", "_id");
```

You can then access the value with `vars.ScummVM["card"].Current|Old` or, conveniently, via `current.card / old.card`.

### Updating the watchers

To keep your watchers updated, place this in `update {}`:

```c#
update {
  vars.ScummVM.Update();
}
```

### Debugging

The following debug flags exist (set them in `startup{}`): 
- `vars.ScummVM.LogResolvedPaths()`: print raw pointer paths any time `Watch<T>` or `Read<T>` are called.
  ```
  [ScummVM-Help] Resolved path: 0x7FF67FF1CA38, 0x200, 0x8, 0x8
  ```
- `vars.ScummVM.LogChangedWatchers()`: print changes in any watchers stored inside the helper's `MemoryWatcherList`.
  ```
  [ScummVM-Help] room: 100 -> 101
  ```

*(Use [TraceSpy](https://github.com/smourier/TraceSpy) (recommended) or [DebugView](https://learn.microsoft.com/en-us/sysinternals/downloads/debugview) to view debug prints)*


## ASLs that use ScummVM-Help

##### Groovie
- [The 7th Guest](https://raw.githubusercontent.com/iamJaffra/ASLs/refs/heads/main/The7thGuest.asl)
- [The 11th Hour](https://raw.githubusercontent.com/iamJaffra/ASLs/refs/heads/main/The11thHour.asl)
##### Mohawk
- [Myst](https://raw.githubusercontent.com/iamJaffra/ASLs/refs/heads/main/Myst.asl)
- [Riven](https://raw.githubusercontent.com/iamJaffra/ASLs/refs/heads/main/Riven.asl)
##### SCI
- [Phantasmagoria](https://raw.githubusercontent.com/iamJaffra/ASLs/refs/heads/main/Phantasmagoria.asl)
- [Shivers](https://raw.githubusercontent.com/iamJaffra/ASLs/refs/heads/main/Shivers.asl)
- [Torin's Passage](https://raw.githubusercontent.com/iamJaffra/ASLs/refs/heads/main/TorinsPassage.asl)
#### Scumm
- [Loom](https://raw.githubusercontent.com/iamJaffra/ASLs/refs/heads/main/Loom.asl)

## Supported engines (work in progress)
- Groovie
- Mohawk_Myst
- Mohawk_Riven
- SCI
- Scumm
- Sword1
