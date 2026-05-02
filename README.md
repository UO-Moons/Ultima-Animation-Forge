# 🛠️ Ultima Animation Forge

<p align="center">
  <img src="Assets/preview.png" alt="Ultima Animation Forge Preview" width="900"/>
</p>

A full-featured animation editor for **Ultima Online**, built for working with both legacy **MUL** animations and modern **UOP** animation files.

Ultima Animation Forge is designed for shard developers, ClassicUO modders, animation artists, and tool developers who need a practical way to view, edit, import, export, and test UO animations.

---

## 🎬 Overview

Ultima Animation Forge lets you work directly with UO animation data files.

It supports:

- Viewing animations
- Editing frames
- Importing new animations
- Exporting animations
- Finding empty MUL slots
- Applying visual effects
- Working with props and overlays
- Comparing animations in a detached preview window

---

## 🚀 Features

### 🎥 Animation Loading

Supports loading animations from:

- `anim.mul`
- `anim.idx`
- `anim2.mul`, `anim3.mul`, etc.
- `AnimationFrame#.uop`
- `AnimationSequence.uop`

The tool can read both MUL and UOP animation sources and display them in one browser.

---

### 🧍 Body, Action, and Direction Support

Animations can be selected by:

- Body ID
- Action
- Direction

Supported animation group sizes:

- 13 actions
- 22 actions
- 35 actions

Directions supported:

- 0 through 4

---

### 🧱 BodyConv and MobTypes Support

Ultima Animation Forge supports:

- `bodyconv.def`
- `mobtypes.txt`

These files help the tool resolve:

- Remapped body IDs
- Correct animation source files
- Monster, animal, human, sea monster, and equipment types

---

### 👁️ Live Preview

The live preview system supports:

- Real-time playback
- Play / pause
- Previous frame
- Next frame
- Playback speed control
- Zoom control
- Checker background
- Frame thumbnails
- Current frame display

---

### 🪟 Detached Preview Window

The detached preview window allows side-by-side comparison.

Use it to:

- Compare two animations
- Keep one animation open while editing another
- Test actions and directions independently from the main preview

---

### ✏️ Frame Editing

Frame editing supports:

- Replacing frames
- Removing frames
- Importing frame sequences
- Editing animation frame data
- Updating frames before saving back to MUL

---

### 📥 Importing

Supported imports include:

- `.vd` files
- PNG frame sequences
- Sprite sheets

Animations can be imported into:

- Existing animations
- Empty MUL slots
- Newly created body slots

---

### 📤 Exporting

Supported exports include:

- `.vd` animation files
- Image sequences
- Individual frames

VD export preserves:

- Actions
- Directions
- Frame data
- Palette data

---

### 🎨 Image Enhancements

Built-in image enhancement tools include:

- Scaling
- Sharpening
- Contrast adjustment
- Hue changes
- Outline effects

Scaling and sharpening support multiple modes, not just numeric values.

Effects can be previewed first, then applied to:

- Current direction
- Full animation

---

### 🎛️ Apply / Reset System

Preview effects are temporary until applied.

Use:

- **Apply To Direction** to apply effects to the current action and direction
- **Apply To Full Animation** to apply effects across the whole animation
- **Reset Preview Effects** to clear temporary preview changes

Important:

> Preview changes are not permanent until applied.

---

### 🧩 Prop System

The prop system allows images to be attached to animations.

Useful for:

- Weapons
- Shields
- Equipment overlays
- Spell effects
- Custom visual attachments

Prop controls include:

- Offset X
- Offset Y
- Scale
- Rotation
- Opacity
- Pivot X
- Pivot Y
- Draw order

Props can be previewed and adjusted frame by frame.

---

### ⚖️ Compare Overlay

Compare overlay lets you place another animation over the current preview.

This helps with:

- Alignment checking
- Pose comparison
- Mount/rider testing
- Verifying body size changes
- Matching animation movement

---

### 🧱 MUL Slot Management

The tool can scan MUL/IDX files and detect empty animation slots.

Supported actions include:

- Finding empty slots
- Importing into empty slots
- Deleting animation slot references
- Creating new MUL/IDX files
- Updating IDX entries safely

---

### 🧬 UOP Support

UOP support includes:

- Hash-based lookup
- UOP frame archive loading
- `AnimationSequence.uop` parsing
- Action remapping
- ZLib decompression
- Mythic compression handling

---

## 🎯 Typical Workflow

```text
Open UO Folder
→ Select Animation
→ Preview
→ Edit Frames / Apply Effects / Add Props
→ Save Changes
→ Export
→ Test In-Game
