-- Supabase Migration: Add song groups

-- 1. Create SongGroups table
CREATE TABLE IF NOT EXISTS "SongGroups" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name" TEXT NOT NULL,
    "CreatedAt" TIMESTAMPTZ DEFAULT now()
);

-- 2. Add GroupId column to Projects table (SongProjects)
ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "GroupId" UUID REFERENCES "SongGroups"("Id") ON DELETE SET NULL;
