# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 GD (Gurekam Dhillon)
"""Retail Melee fighter table: internal kind, file code and the PlCo common-part joint names.

Vendored (trimmed) from the author's build_melee_fighter.py: only FIGHTERS and COMMON are used by Skin Fitter.
"""
# Clones without their own submotion enum (Dr. Mario, Falco, Ganondorf, Young Link, Pichu, Roy, Nana) list their base
# fighter's prefix; costume tools (Skin Fitter) use only the kind and file code.
FIGHTERS = {"mario": (0, "Mr", "ftMario", "ftMr"), "fox": (1, "Fx", "ftFox", "ftFx"),
            "falcon": (2, "Ca", "ftCaptain", "ftCa"), "captain": (2, "Ca", "ftCaptain", "ftCa"),
            "dk": (3, "Dk", "ftDonkey", "ftDk"), "kirby": (4, "Kb", "ftKirby", "ftKb"), "bowser": (5, "Kp", "ftKoopa", "ftKp"),
            "link": (6, "Lk", "ftLink", "ftLk"), "sheik": (7, "Sk", "ftSeak", "ftSk"), "ness": (8, "Ns", "ftNess", "ftNs"),
            "peach": (9, "Pe", "ftPeach", "ftPe"), "popo": (10, "Pp", "ftPopo", "ftPp"), "nana": (11, "Nn", "ftNana", "ftPp"),
            "pikachu": (12, "Pk", "ftPikachu", "ftPk"), "samus": (13, "Ss", "ftSamus", "ftSs"), "yoshi": (14, "Ys", "ftYoshi", "ftYs"),
            "jigglypuff": (15, "Pr", "ftPurin", "ftPr"), "mewtwo": (16, "Mt", "ftMewtwo", "ftMt"), "luigi": (17, "Lg", "ftLuigi", "ftLg"),
            "marth": (18, "Ms", "ftMars", "ftMs"), "zelda": (19, "Zd", "ftZelda", "ftZd"), "younglink": (20, "Cl", "ftCLink", "ftLk"),
            "drmario": (21, "Dr", "ftDrMario", "ftMr"), "falco": (22, "Fc", "ftFalco", "ftFx"), "pichu": (23, "Pc", "ftPichu", "ftPk"),
            "gameandwatch": (24, "Gw", "ftGameWatch", "ftGw"), "ganondorf": (25, "Gn", "ftGanon", "ftCa"), "roy": (26, "Fe", "ftEmblem", "ftMs")}

# Common part ids as PlCo's joint_to_part holds them. The decomp's Fighter_Part enum
# (ft/forward.h) has one entry fewer (no WaistB) and so is off by one from 16 on: Kirby's
# common part 52 hangs off YRotN like every fighter's ThrowN, where the enum says TransN2.
COMMON = ["TopN", "TransN", "XRotN", "YRotN", "HipN", "WaistN",
          "LLegJA", "LLegJ", "LKneeJ", "LFootJA", "LFootJ",
          "RLegJA", "RLegJ", "RKneeJ", "RFootJA", "RFootJ",
          "WaistB", "BustN",
          "LShoulderN", "LShoulderJA", "LShoulderJ", "LArmJ", "LHandN",
          "L1stNa", "L1stNb", "L2ndNa", "L2ndNb", "L3rdNa", "L3rdNb", "L4thNa", "L4thNb",
          "LHaveN", "LThumbNa", "LThumbNb", "NeckN", "HeadN",
          "RShoulderN", "RShoulderJA", "RShoulderJ", "RArmJ", "RHandN",
          "R1stNa", "R1stNb", "R2ndNa", "R2ndNb", "R3rdNa", "R3rdNb", "R4thNa", "R4thNb",
          "RHaveN", "RThumbNa", "RThumbNb", "ThrowN", "ExtraN"]
# Engine-neutral IR roles (assets.schema.json BoneRole) for the common parts that have one.
