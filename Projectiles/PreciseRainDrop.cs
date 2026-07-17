using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace JoJoStands.Projectiles
{
    public class PreciseRainDrop : ModProjectile
    {
        public override string Texture => "JoJoStands/Projectiles/RainDrop";

        private bool firstFrame = true;
        private Vector2 lastAnchorPos = Vector2.Zero;

        private bool fading = false;
        private int fadeAge = 0;
        private Vector2 fadeStopCenter = Vector2.Zero;
        private const int FadeFrames = 8;

        private bool inWallPhase = false;
        private Vector2 prevCenter = Vector2.Zero;

        private struct WallSpark
        {
            public Vector2 pos;
            public Vector2 vel;
            public float scale;
            public float rot;
            public int life;
            public int maxLife;
        }
        private System.Collections.Generic.List<WallSpark> wallSparks = new System.Collections.Generic.List<WallSpark>();

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 40;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 6;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -2;
            Projectile.timeLeft = 200;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        private bool TryGetAnchor(out Vector2 anchorCenter)
        {
            if (Projectile.owner >= 0 && Projectile.owner < Main.player.Length)
            {
                Player p = Main.player[Projectile.owner];
                if (p != null && p.active && !p.dead)
                {
                    anchorCenter = p.Center;
                    return true;
                }
            }
            anchorCenter = Vector2.Zero;
            return false;
        }

        public override void AI()
        {
            Vector2 frameStartCenter = Projectile.Center;

            for (int i = wallSparks.Count - 1; i >= 0; i--)
            {
                WallSpark s = wallSparks[i];
                s.pos += s.vel;
                s.vel *= 0.92f;
                s.life -= 1;
                if (s.life <= 0) wallSparks.RemoveAt(i);
                else wallSparks[i] = s;
            }

            if (firstFrame)
            {
                firstFrame = false;
                if (TryGetAnchor(out Vector2 a0)) lastAnchorPos = a0;
                else lastAnchorPos = Projectile.Center;
                inWallPhase = HitboxInSolidAt(Projectile.position);
                prevCenter = Projectile.Center;
            }
            else if (!fading && TryGetAnchor(out Vector2 anchorNow))
            {
                Vector2 anchorDelta = anchorNow - lastAnchorPos;
                if (anchorDelta != Vector2.Zero)
                {
                    Projectile.position += anchorDelta;
                    int len = Projectile.oldPos.Length;
                    for (int i = 0; i < len; i++)
                    {
                        if (Projectile.oldPos[i] != Vector2.Zero)
                            Projectile.oldPos[i] += anchorDelta;
                    }
                }
                lastAnchorPos = anchorNow;
            }

            if (inWallPhase)
            {
                Vector2 wpDelta = frameStartCenter - prevCenter;
                float wpDist = wpDelta.Length();
                if (wpDist > 0.5f)
                {
                    Vector2 wpDir = wpDelta / wpDist;
                    bool wpAir = false;
                    for (float t = 0f; t <= wpDist; t += 2f)
                    {
                        bool solid = PointInSolid(prevCenter + wpDir * t);
                        if (!wpAir && !solid) wpAir = true;
                        else if (wpAir && solid)
                        {
                            StartFading(t >= 2f ? prevCenter + wpDir * (t - 2f) : prevCenter);
                            prevCenter = frameStartCenter;
                            return;
                        }
                    }
                }
                if (!HitboxInSolidAt(Projectile.position))
                    inWallPhase = false;
                else
                {
                    for (int i = 0; i < 2; i++)
                    {
                        WallSpark s = new WallSpark();
                        s.pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-7f, 7f));
                        s.vel = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.5f, 0.5f));
                        s.scale = Main.rand.NextFloat(0.18f, 0.30f);
                        s.rot = Main.rand.NextFloat(MathHelper.TwoPi);
                        s.maxLife = Main.rand.Next(14, 22);
                        s.life = s.maxLife;
                        wallSparks.Add(s);
                    }
                }
            }

            if (fading)
            {
                Projectile.velocity *= 0.55f;
                fadeAge++;
                if (fadeAge >= FadeFrames)
                {
                    Projectile.Kill();
                    return;
                }
                float lf = 1f - fadeAge / (float)FadeFrames;
                Lighting.AddLight(fadeStopCenter, 0.05f * lf, 0.10f * lf, 0.18f * lf);
                prevCenter = frameStartCenter;
                return;
            }

            Projectile.velocity.Y += 0.92f;
            if (Projectile.velocity.Y > 45f) Projectile.velocity.Y = 45f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(3))
            {
                int d = Dust.NewDust(Projectile.Center - new Vector2(3f), 6, 6,
                    DustID.Water, 0f, 0f, 120,
                    new Color(140, 210, 255), Main.rand.NextFloat(0.7f, 0.9f));
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.25f;
            }
            Lighting.AddLight(Projectile.Center, 0.05f, 0.10f, 0.18f);
            Projectile.tileCollide = !inWallPhase;
            prevCenter = frameStartCenter;
        }

        private void StartFading(Vector2 fadeCenter)
        {
            if (fading) return;
            fading = true;
            fadeAge = 0;
            fadeStopCenter = fadeCenter;
            Projectile.friendly = false;
            Projectile.damage = 0;
            Projectile.velocity = Vector2.Zero;
        }

        private bool HitboxInSolidAt(Vector2 topLeft)
        {
            int x1 = (int)topLeft.X;
            int y1 = (int)topLeft.Y;
            int x2 = x1 + Projectile.width - 1;
            int y2 = y1 + Projectile.height - 1;
            int tileX1 = x1 / 16;
            int tileY1 = y1 / 16;
            int tileX2 = x2 / 16;
            int tileY2 = y2 / 16;
            for (int tx = tileX1; tx <= tileX2; tx++)
            {
                for (int ty = tileY1; ty <= tileY2; ty++)
                {
                    if (tx < 0 || ty < 0 || tx >= Main.maxTilesX || ty >= Main.maxTilesY) continue;
                    Tile t = Main.tile[tx, ty];
                    if (t == null || !t.HasTile) continue;
                    if (Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                        return true;
                }
            }
            return false;
        }

        private bool PointInSolid(Vector2 worldPos)
        {
            int tx = (int)(worldPos.X / 16f);
            int ty = (int)(worldPos.Y / 16f);
            if (tx < 0 || ty < 0 || tx >= Main.maxTilesX || ty >= Main.maxTilesY) return false;
            Tile t = Main.tile[tx, ty];
            if (t == null || !t.HasTile) return false;
            return Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType];
        }

        public override bool? CanHitNPC(NPC target)
        {
            if (fading) return false;
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (fading) return false;
            if (projHitbox.Intersects(targetHitbox)) return true;
            if (prevCenter != Vector2.Zero)
            {
                float collisionPoint = 0f;
                if (Collision.CheckAABBvLineCollision(
                    new Vector2(targetHitbox.X, targetHitbox.Y),
                    new Vector2(targetHitbox.Width, targetHitbox.Height),
                    prevCenter, Projectile.Center,
                    Math.Max(Projectile.width, Projectile.height) * 0.5f,
                    ref collisionPoint))
                    return true;
            }
            return null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (fading) return;
            Vector2 snapCenter = prevCenter == Vector2.Zero ? Projectile.Center : prevCenter;
            Projectile.position = snapCenter - Projectile.Size * 0.5f;
            if (Projectile.oldPos.Length > 0)
                Projectile.oldPos[0] = Projectile.position;
            StartFading(snapCenter);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Projectile.velocity = oldVelocity;
            StartFading(Projectile.Center);
            return false;
        }

        private bool ClipSpriteSegment(Vector2 dirVec, float texHeight, ref Vector2 center, ref float stretch)
        {
            if (PointInSolid(center)) return false;

            float halfLen = (texHeight * 0.5f) * stretch;
            if (halfLen < 0.5f) return true;

            Vector2 endBack = center - dirVec * halfLen;
            float fullLen = halfLen * 2f;
            const float step = 2f;

            float visStart = 0f;
            if (PointInSolid(endBack))
            {
                bool cleared = false;
                for (float t = step; t <= fullLen; t += step)
                {
                    if (!PointInSolid(endBack + dirVec * t))
                    {
                        visStart = t;
                        cleared = true;
                        break;
                    }
                }
                if (!cleared) return false;
            }

            float visEnd = fullLen;
            for (float t = visStart + step; t <= fullLen; t += step)
            {
                if (PointInSolid(endBack + dirVec * t))
                {
                    visEnd = t - step;
                    break;
                }
            }

            float visLen = visEnd - visStart;
            if (visLen < 1f) return false;

            center = endBack + dirVec * (visStart + visLen * 0.5f);
            stretch = visLen / texHeight;
            return true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Main.netMode == NetmodeID.Server) return false;

            Texture2D sparkTex = TextureAssets.Dust.Value;
            Vector2 sparkOrigin = new Vector2(5f, 5f);
            Rectangle sparkRect = new Rectangle(290, 0, 10, 10);
            for (int i = 0; i < wallSparks.Count; i++)
            {
                WallSpark s = wallSparks[i];
                float lifeFrac = s.maxLife > 0 ? s.life / (float)s.maxLife : 0f;
                Color sparkColor = new Color(220, 240, 255, 240) * lifeFrac;
                Main.EntitySpriteDraw(sparkTex, s.pos - Main.screenPosition, sparkRect,
                    sparkColor, s.rot, sparkOrigin,
                    new Vector2(s.scale * 2f, s.scale * 2f), SpriteEffects.None, 0);
            }

            if (inWallPhase) return false;

            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            float fadeAlpha = fading
                ? MathHelper.Clamp(1f - fadeAge / (float)FadeFrames, 0f, 1f)
                : 1f;

            DrawTrailLayer(tex, origin, new Color(60, 145, 255, 80) * fadeAlpha, 0.55f);
            DrawTrailLayer(tex, origin, new Color(190, 230, 255, 220) * fadeAlpha, 0.25f);

            if (!fading)
            {
                Vector2 headCenter = Projectile.Center;
                float speed = Projectile.velocity.Length();
                float tipS = MathHelper.Clamp(speed / 4f, 1.4f, 6f);
                Vector2 headDir = speed > 0.01f ? Projectile.velocity / speed : Vector2.UnitY;

                if (ClipSpriteSegment(headDir, tex.Height, ref headCenter, ref tipS))
                {
                    Main.EntitySpriteDraw(tex, headCenter - Main.screenPosition, null,
                        new Color(220, 240, 255, 240), Projectile.rotation, origin,
                        new Vector2(0.32f, tipS), SpriteEffects.None, 0);
                }
            }

            return false;
        }

        private void DrawTrailLayer(Texture2D tex, Vector2 origin, Color baseColor, float widthScale)
        {
            int len = Projectile.oldPos.Length;
            float texHeight = tex.Height;

            for (int i = 0; i < len - 1; i++)
            {
                Vector2 cur = Projectile.oldPos[i];
                Vector2 nxt = Projectile.oldPos[i + 1];
                if (cur == Vector2.Zero || nxt == Vector2.Zero) continue;

                Vector2 curCenter = cur + Projectile.Size * 0.5f;

                Vector2 delta = cur - nxt;
                float dl = delta.Length();
                if (dl < 0.5f) continue;

                float rot = delta.ToRotation() + MathHelper.PiOver2;
                float fade = 1f - (i / (float)len) * 0.85f;
                float stretch = MathHelper.Clamp(dl / 4f, 1.2f, 6f);
                Vector2 dirVec = delta / dl;

                Vector2 drawCenter = curCenter;
                float drawStretch = stretch;

                if (!ClipSpriteSegment(dirVec, texHeight, ref drawCenter, ref drawStretch))
                    continue;

                Vector2 dPos = drawCenter - Main.screenPosition;
                Main.EntitySpriteDraw(tex, dPos, null,
                    baseColor * fade, rot, origin,
                    new Vector2(widthScale, drawStretch), SpriteEffects.None, 0);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            Player owner = Main.player[Projectile.owner];
            MyPlayer mp = owner.GetModPlayer<MyPlayer>();
            bool crit = Main.rand.NextFloat(1, 100 + 1) <= mp.standCritChangeBoosts;
            if (crit) modifiers.SetCrit();
            modifiers.SourceDamage *= mp.standDamageBoosts * 0.85f;
        }
    }
}
