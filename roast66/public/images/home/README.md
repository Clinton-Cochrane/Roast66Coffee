# Homepage photography

Use only owner-approved Roast 66 photography. Do not commit stock, generated,
unlicensed, or full-resolution source files.

## What you need

The current homepage renders two photo slots:

1. `hero`: the Roast 66 trailer serving drinks at a real pop-up, with the
   trailer immediately recognizable.
2. `marketing`: the owner working in or beside the trailer, or a second trailer
   photo that supports the owner-led story.

You need one original photo for each slot. Each original is exported to seven
optimized files, so filling both slots produces 14 committed image files.

`HomePhoto.tsx` also recognizes the name `story`, but no current page renders
that slot. Do not create `story-*` files unless a story photo is added to a page.

## Crop and composition

### Hero

- Crop to landscape 3:2.
- Keep the trailer or main subject inside the center 60% of the frame so the
  responsive `object-fit: cover` treatment works on narrow screens.
- Export at 640 x 427, 960 x 640, and 1440 x 960 pixels.

### Marketing

- Crop to 40:21, matching the current 1200 x 630 display ratio.
- Keep the person or trailer away from the extreme edges.
- Export at 640 x 336, 960 x 504, and 1440 x 756 pixels.

## Required filenames

Place the finished files in this directory. Keep the names and lowercase file
extensions exactly as shown.

```text
hero-640.avif
hero-640.webp
hero-960.avif
hero-960.webp
hero-960.jpg
hero-1440.avif
hero-1440.webp

marketing-640.avif
marketing-640.webp
marketing-960.avif
marketing-960.webp
marketing-960.jpg
marketing-1440.avif
marketing-1440.webp
```

The browser chooses one appropriately sized AVIF or WebP file. The 960-pixel
JPEG is a compatibility fallback. These are different exports of the same two
photos, not 14 different photographs.

## Make the files with ImageMagick

The repository workstation has ImageMagick's `convert` command with AVIF,
WebP, and JPEG support. Keep the original photos outside the repository so they
cannot be committed accidentally. For example:

```bash
mkdir -p /tmp/roast66-home-photo-sources
```

Copy the originals there as `hero-original.jpg` and
`marketing-original.jpg`. The input extension can be changed in the commands
if the originals are PNG, HEIC, or another supported format.

From the repository root, paste this function into the terminal:

```bash
make_home_photo() {
  local source_file="$1"
  local photo_name="$2"
  local photo_output_dir="roast66/public/images/home"
  local jpeg_size
  local photo_dimensions
  local photo_width

  case "$photo_name" in
    hero)
      set -- 640x427 960x640 1440x960
      jpeg_size="960x640"
      ;;
    marketing)
      set -- 640x336 960x504 1440x756
      jpeg_size="960x504"
      ;;
    *)
      echo "Expected photo name: hero or marketing" >&2
      return 1
      ;;
  esac

  for photo_dimensions in "$@"; do
    photo_width="${photo_dimensions%%x*}"

    convert "$source_file" \
      -auto-orient -strip \
      -resize "${photo_dimensions}^" \
      -gravity center \
      -extent "$photo_dimensions" \
      -quality 50 \
      "$photo_output_dir/$photo_name-$photo_width.avif"

    convert "$source_file" \
      -auto-orient -strip \
      -resize "${photo_dimensions}^" \
      -gravity center \
      -extent "$photo_dimensions" \
      -quality 78 \
      "$photo_output_dir/$photo_name-$photo_width.webp"
  done

  convert "$source_file" \
    -auto-orient -strip \
    -resize "${jpeg_size}^" \
    -gravity center \
    -extent "$jpeg_size" \
    -sampling-factor 4:2:0 \
    -quality 82 \
    "$photo_output_dir/$photo_name-960.jpg"
}
```

Then generate both sets:

```bash
make_home_photo /tmp/roast66-home-photo-sources/hero-original.jpg hero
make_home_photo /tmp/roast66-home-photo-sources/marketing-original.jpg marketing
```

The crop is centered automatically. If that cuts off the subject, crop the
original manually to the required aspect ratio first, then run the commands
again.

## Verify the result

Confirm the files and dimensions:

```bash
rg --files roast66/public/images/home
identify roast66/public/images/home/*.{avif,webp,jpg}
```

Open the homepage at desktop and mobile widths and check that:

- the important subject remains visible;
- neither photo looks stretched or blurry;
- loading the photos does not shift the surrounding layout;
- no horizontal scrollbar appears;
- the page still makes sense if an image fails to load.

Finally, run the frontend checks:

```bash
npm --prefix roast66 test
npm --prefix roast66 run lint
npm --prefix roast66 run build
git status --short
```

Only this README and the 14 optimized files should be part of the photo change.
Do not commit the original full-resolution photographs or image metadata.
