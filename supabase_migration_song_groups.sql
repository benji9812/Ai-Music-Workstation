-- Supabase Migration: Add song groups

-- 1. Create song_groups table
CREATE TABLE IF NOT EXISTS song_groups (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    user_id UUID REFERENCES auth.users(id),
    created_at TIMESTAMPTZ DEFAULT now()
);

-- 2. Add group_id column to songs table
ALTER TABLE songs
ADD COLUMN IF NOT EXISTS group_id UUID REFERENCES song_groups(id) ON DELETE SET NULL;
