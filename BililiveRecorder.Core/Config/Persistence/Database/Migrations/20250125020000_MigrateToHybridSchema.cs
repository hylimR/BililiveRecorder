#if NET8_0_OR_GREATER
using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable disable

namespace BililiveRecorder.Core.Config.Persistence.Database.Migrations
{
    /// <inheritdoc />
    public partial class MigrateToHybridSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration moves data from the old JSON blob schema (config table)
            // to the new hybrid schema (global_config + rooms tables)

            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    config_record RECORD;
                    global_json JSONB;
                    rooms_json JSONB;
                    room_item JSONB;
                    v_room_url TEXT;
                    v_platform INT;
                    v_auto_record BOOLEAN;
                    v_config_overrides JSONB;
                BEGIN
                    -- Check if old config table has data
                    SELECT global_config::JSONB, rooms_config::JSONB
                    INTO global_json, rooms_json
                    FROM recorder.config
                    WHERE id = 1;

                    IF global_json IS NOT NULL THEN
                        RAISE NOTICE 'Migrating global config from JSON blob to key-value table';

                        -- Migrate global config to key-value table
                        -- Insert each top-level property as a key-value pair
                        FOR config_record IN
                            SELECT key, value
                            FROM jsonb_each(global_json)
                        LOOP
                            INSERT INTO recorder.global_config (key, value, value_type, created_at, updated_at)
                            VALUES (
                                config_record.key,
                                config_record.value #>> '{}',  -- Extract string value
                                CASE
                                    WHEN jsonb_typeof(config_record.value) = 'boolean' THEN 'bool'
                                    WHEN jsonb_typeof(config_record.value) = 'number' THEN 'int'
                                    ELSE 'string'
                                END,
                                NOW(),
                                NOW()
                            )
                            ON CONFLICT (key) DO UPDATE
                            SET value = EXCLUDED.value,
                                value_type = EXCLUDED.value_type,
                                updated_at = NOW();
                        END LOOP;

                        RAISE NOTICE 'Global config migrated successfully';
                    END IF;

                    IF rooms_json IS NOT NULL THEN
                        RAISE NOTICE 'Migrating rooms from JSON array to rooms table';

                        -- Migrate rooms from JSON array to rooms table
                        FOR room_item IN
                            SELECT * FROM jsonb_array_elements(rooms_json)
                        LOOP
                            -- Extract core fields
                            v_room_url := room_item->>'RoomUrl';

                            -- Handle legacy RoomId (if RoomUrl is missing, construct it from RoomId)
                            IF v_room_url IS NULL OR v_room_url = '' THEN
                                IF room_item->>'RoomId' IS NOT NULL THEN
                                    -- Construct URL from RoomId (assume Bilibili if platform not specified)
                                    v_platform := COALESCE((room_item->>'Platform')::INT, 0);
                                    IF v_platform = 0 THEN
                                        v_room_url := 'https://live.bilibili.com/' || (room_item->>'RoomId');
                                    ELSIF v_platform = 1 THEN
                                        v_room_url := 'https://live.douyin.com/' || (room_item->>'RoomId');
                                    ELSE
                                        v_room_url := 'https://live.bilibili.com/' || (room_item->>'RoomId');
                                    END IF;
                                ELSE
                                    -- Skip this room if both RoomUrl and RoomId are missing
                                    RAISE NOTICE 'Skipping room with no RoomUrl or RoomId: %', room_item;
                                    CONTINUE;
                                END IF;
                            END IF;

                            -- Handle platform (default to 0 if not set)
                            v_platform := COALESCE((room_item->>'Platform')::INT, 0);

                            -- Handle AutoRecord (extract from HierarchicalPropertyDefault if present)
                            IF room_item->'AutoRecord'->'Value' IS NOT NULL THEN
                                v_auto_record := (room_item->'AutoRecord'->>'Value')::BOOLEAN;
                            ELSE
                                v_auto_record := true;  -- Default value
                            END IF;

                            -- Build config_overrides JSONB (all properties except core fields)
                            v_config_overrides := room_item - 'RoomUrl' - 'RoomId' - 'Platform' - 'AutoRecord';

                            -- Remove empty overrides
                            IF v_config_overrides = '{}'::JSONB THEN
                                v_config_overrides := NULL;
                            END IF;

                            -- Insert or update room
                            INSERT INTO recorder.rooms (
                                room_url,
                                platform,
                                auto_record,
                                enabled,
                                config_overrides,
                                created_at,
                                updated_at
                            )
                            VALUES (
                                v_room_url,
                                v_platform,
                                v_auto_record,
                                true,
                                v_config_overrides,
                                NOW(),
                                NOW()
                            )
                            ON CONFLICT (room_url, platform) DO UPDATE
                            SET auto_record = EXCLUDED.auto_record,
                                config_overrides = EXCLUDED.config_overrides,
                                updated_at = NOW();
                        END LOOP;

                        RAISE NOTICE 'Rooms migrated successfully';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                -- Mark old config table as migrated (add a flag or comment)
                COMMENT ON TABLE recorder.config IS 'Deprecated - migrated to hybrid schema (global_config + rooms tables)';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse migration: Move data from hybrid schema back to JSON blob
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    global_obj JSONB := '{}'::JSONB;
                    rooms_arr JSONB := '[]'::JSONB;
                    config_record RECORD;
                    room_record RECORD;
                    room_obj JSONB;
                BEGIN
                    RAISE NOTICE 'Migrating global config back to JSON blob';

                    -- Build global config JSON object from key-value table
                    FOR config_record IN
                        SELECT key, value, value_type FROM recorder.global_config
                    LOOP
                        global_obj := global_obj || jsonb_build_object(
                            config_record.key,
                            CASE config_record.value_type
                                WHEN 'bool' THEN to_jsonb(config_record.value::BOOLEAN)
                                WHEN 'int' THEN to_jsonb(config_record.value::INT)
                                ELSE to_jsonb(config_record.value)
                            END
                        );
                    END LOOP;

                    RAISE NOTICE 'Migrating rooms back to JSON array';

                    -- Build rooms JSON array from rooms table
                    FOR room_record IN
                        SELECT room_url, platform, auto_record, config_overrides
                        FROM recorder.rooms
                        ORDER BY id
                    LOOP
                        room_obj := jsonb_build_object(
                            'RoomUrl', room_record.room_url,
                            'Platform', room_record.platform,
                            'AutoRecord', jsonb_build_object('HasValue', true, 'Value', room_record.auto_record)
                        );

                        -- Merge config_overrides if present
                        IF room_record.config_overrides IS NOT NULL THEN
                            room_obj := room_obj || room_record.config_overrides;
                        END IF;

                        rooms_arr := rooms_arr || room_obj;
                    END LOOP;

                    -- Update config table with migrated data
                    UPDATE recorder.config
                    SET global_config = global_obj::TEXT,
                        rooms_config = rooms_arr::TEXT,
                        updated_at = NOW()
                    WHERE id = 1;

                    RAISE NOTICE 'Migration reversed successfully';
                END $$;
            ");

            migrationBuilder.Sql(@"
                COMMENT ON TABLE recorder.config IS NULL;
            ");
        }
    }
}
#endif
