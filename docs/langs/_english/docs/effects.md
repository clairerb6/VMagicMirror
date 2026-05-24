---
layout: page
title: Effects
lang: en
---

# Effects

`Effects` tab supports whole image quality, light, shadow, bloom, outline, rim light and wind settings.

<div class="row">
{% include docimg.html file="/images/docs/effects_top_1.png" customclass="col s12 m6 l6" imgclass="fit-doc-img" %}
{% include docimg.html file="/images/docs/effects_top_2.png" customclass="col s12 m6 l6" imgclass="fit-doc-img" %}
</div>

#### 1. Settings
{: .doc-sec2 }

`Image Quality`: Select whole image quality and framerate. Turn on `Disable HDR even when Quality is High or higher` to render without HDR. Turning off HDR saves performance, while lighting effect quality becomes lower.

`Anti Alias`: Choose anti alias (Multisample Anti-Alias) option. Note that higher setting leads more computational load. The feature is off by default.

`Low FPS for Bone Motion`: Turn on to reduce avatar motion's FPS.  Note that this option does not reduce CPU usage.

`Light`: Support color, intensity, and direction of light settings. `Desktop Color Based Lighting` applies whole monitor's content color dynamically.

`Shadow`: Enable / disable shadow, and setup shadow color, blur size, intensity, direction, and depth offset.

`Bloom`: Color and intensity of bloom.

`Outline`: Available from v3.6.0. Supports outline width, color and quality. Outline effect is applied when `Transparent Window` is enabled. Note that, when there is almost-opaque visual (including bloom and shadow), outline will also applied to those elements.

`Rim Light`: Effect to light around the avatar's outline. Supports rim light color, emission, intensity, thickness and angle.

`Ambient Occlusion`: Effect to darken dents and boundaries around avatar parts. Supports enabling / disabling the effect and intensity.

`Wind`: Strength and direction of wind.

<div class="note-area" markdown="1">

**NOTE**

`Desktop Color Based Lighting` option uses a kind of window capture API and it leads yellow frame effect on your monitor. 

</div>

v4.2.0 and later version supports `Full Body Shadow Settings` option in `Shadow`. Full Body Shadow is the shadow which is shown on the ground level, instead of avatar's back. This option is designed to achieve good appearance when showing avatar's full body.

By default, Full Body Shadow is enabled when avatar's lower body is controlled by [Game Input](../game_input) or [VMC Protocol](../vmc_protocol). Turn on `Always Apply Full Body Shadow` to show Full Body Shadow regardless of lower body motion state.


#### 2. Hint
{: .doc-sec2 }

Light and shadow has separated orientations, so you can set the light orientation simply for the avatar's looking, while adjust shadow orientation to show it on the back of the avatar.

Also you can adjust the depth offset and orientation of the shadow to , so that your avatar looks near or far to the screen.

Below is default setting, and example setting to show distance between screen and the avatar.

<div class="row">
{% include docimg.html file="/images/docs/shadow_default.png" customclass="col s12 m4 l4" imgclass="fit-doc-img" %}
{% include docimg.html file="/images/docs/shadow_look_far.png" customclass="col s12 m4 l4" imgclass="fit-doc-img" %}
</div>

Please be aware that some VRM avatar uses `Unlit` type shader, to which the light setting has no effect.

For the wind settings:

1. Please setup `VRMSpringBone` beforehand, to enable wind-based motion.
2. Wind feature moves all `VRMSpringBone` components, so "only hair (not skirt)" like setting is not supported.
