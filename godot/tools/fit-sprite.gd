extends SceneTree
##
## Fit a unit's sprite strips to the pipeline's anchoring rule, in place.
##
##     ./fit-sprite.sh presentation/units/arrow-tower [--dry-run]
##
## The rule, from SpriteUnitView.ApplyTransform: the quad is a SQUARE of side
## `frameCells`, lifted so its BOTTOM EDGE sits on the ground at the cell centre.
## Three things follow, and "trim to all edges" gets two of them wrong:
##
##   1. Frames must stay SQUARE. Frame count is inferred as width / height, so a
##      trimmed-to-content strip is silently re-read as a different number of
##      frames -- a 262x662 image is not a tall sprite, it is a zero-frame one.
##   2. The subject must be HORIZONTALLY CENTRED, because the frame's centre line
##      is the cell centre. Trimming an asymmetric subject to its own bounds
##      shifts it off the tile.
##   3. The subject's base must touch the BOTTOM EDGE. Any transparent gap there
##      is float: the unit hovers above the board by gap/side * frameCells cells.
##
## And one that only shows up in motion: the crop box is computed ONCE across
## every frame of every clip in the folder, never per frame or per file. Cropping
## each frame to its own bounds pins the subject in place and makes the animation
## jitter; cropping each clip separately makes the unit change size when it fires.
##
## Prints the `frameCells` that preserves the current on-screen size. It does not
## edit unit.json -- that number is an art decision and this tool only knows how
## to keep it where it was.

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	var dry := args.has("--dry-run")
	var targets: Array[String] = []
	for a in args:
		if not a.begins_with("--"):
			targets.append(a)

	if targets.is_empty():
		printerr("usage: fit-sprite.sh <unit-folder> [more folders] [--dry-run]")
		quit(2)
		return

	var failed := 0
	for folder in targets:
		if not _fit(ProjectSettings.globalize_path(folder), dry):
			failed += 1
	quit(1 if failed > 0 else 0)


func _fit(folder: String, dry: bool) -> bool:
	var dir := DirAccess.open(folder)
	if dir == null:
		printerr("cannot open %s" % folder)
		return false

	# Same clip set the loader knows, same formats it globs.
	var clips := ["idle", "move", "fire", "hit", "death"]
	var files: Array[String] = []
	for f in dir.get_files():
		var ext := f.get_extension().to_lower()
		if ext != "png" and ext != "webp":
			continue
		if not clips.has(f.get_basename().to_lower()):
			continue
		files.append(folder.path_join(f))
	files.sort()

	if files.is_empty():
		printerr("%s: no standard clip strips" % folder)
		return false

	# ---- one crop box for the whole unit -------------------------------------
	var images := {}
	var side := 0
	var union := Rect2i()
	var have_union := false

	for path in files:
		var img := Image.load_from_file(path)
		if img == null:
			printerr("%s: will not load" % path)
			return false
		img.convert(Image.FORMAT_RGBA8)
		images[path] = img

		var w := img.get_width()
		var h := img.get_height()
		if w % h != 0:
			printerr("%s: %dx%d is not a whole number of square frames" % [path, w, h])
			return false
		if side == 0:
			side = h
		elif side != h:
			printerr("%s: frame side %d differs from %d in the same folder" % [path, h, side])
			return false

		for i in range(w / h):
			var frame := img.get_region(Rect2i(i * h, 0, h, h))
			var used := frame.get_used_rect()
			if used.size.x <= 0 or used.size.y <= 0:
				continue
			if have_union:
				union = union.merge(used)
			else:
				union = used
				have_union = true

	if not have_union:
		printerr("%s: every frame is fully transparent" % folder)
		return false

	var new_side: int = maxi(union.size.x, union.size.y)
	var scale := float(new_side) / float(side)

	print("%s" % folder.get_file())
	print("  frame       %d -> %d px   (content %dx%d)" % [side, new_side, union.size.x, union.size.y])
	print("  float       %d px of empty below the base%s"
		% [side - union.end.y, "" if side - union.end.y > 0 else " (already seated)"])
	print("  frameCells  multiply the current value by %.4f" % scale)

	if dry:
		print("  dry run, nothing written")
		return true

	# ---- rewrite each strip --------------------------------------------------
	for path in files:
		var img: Image = images[path]
		var frames := img.get_width() / side
		var out := Image.create(new_side * frames, new_side, false, Image.FORMAT_RGBA8)
		out.fill(Color(0, 0, 0, 0))

		# Centred horizontally, base flush to the bottom edge -- rules 2 and 3.
		var dx := (new_side - union.size.x) / 2
		var dy := new_side - union.size.y

		for i in range(frames):
			var src := Rect2i(i * side + union.position.x, union.position.y, union.size.x, union.size.y)
			out.blit_rect(img, src, Vector2i(i * new_side + dx, dy))

		var err: int
		if path.get_extension().to_lower() == "webp":
			err = out.save_webp(path, false)   # lossless: this is a re-save of a re-save
		else:
			err = out.save_png(path)
		if err != OK:
			printerr("  %s: save failed (%d)" % [path.get_file(), err])
			return false
		print("  wrote %s  %dx%d (%d frame%s)"
			% [path.get_file(), new_side * frames, new_side, frames, "" if frames == 1 else "s"])

	return true
