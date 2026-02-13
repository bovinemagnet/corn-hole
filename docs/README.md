# Strategic Documentation

This folder contains architectural vision, planning documents, and long-term roadmap for the Corn Hole multiplayer game project.

## 📖 Documentation Index

### Start Here

- **[PRD (Product Requirements Document)](prd-1.md)** - Complete product vision, requirements, and phased development plan
- **[Backlog](backlog.md)** - User stories mapped to phases with acceptance criteria
- **[One-Page Checklist](one-page-checklist)** - Quick phase-by-phase implementation guide

### Architecture & Design

- **[Architectural Overview](arch_overview.md)** - Photon Fusion strategy and migration path from Host to Server Mode
- **[Authoritative Event Model](overview_rules.md)** - Server authority rules, anti-cheat, and network trust model
- **[Folder Structure](folder_structure.md)** - Unity project organization and assembly definition layout
- **[Project Structures](structures.md)** - Assembly definition JSON and boot flow patterns
- **[ConsumedSet Sync](consumedSet_sync.md)** - Advanced object synchronization strategy with bitsets

### Diagrams

- **[Minimal Component Architecture](Minimal_component_architecture.mmd)** - Component relationship diagram
- **[Server Tick Pipeline](server_tick.mmd)** - Server tick processing flowchart
- **[Event Flow Sequence](event_flow_sequence.mmd)** - Client-server interaction sequence diagram

## 🔄 Relationship to Implementation Guides

The strategic documentation in this folder describes:
- **What** we're building (product vision)
- **Why** we're building it this way (architectural decisions)
- **When** features will be implemented (phased roadmap)

The implementation guides in the root directory provide:
- **How** to build the current implementation (code patterns)
- **Quick start** guides for developers (setup and troubleshooting)
- **Practical details** for Phase 0-1 (current state)

**See the root [README.md](../README.md) and [QUICKSTART.md](../QUICKSTART.md) for hands-on implementation guides.**

## 📊 Current Implementation Status

This project is being built in phases:

| Phase | Status | Description | Documentation |
|-------|--------|-------------|---------------|
| **Phase 0** | ✅ Complete | Single-player prototype with core mechanics | [Backlog](backlog.md) Phase 0 |
| **Phase 1** | ✅ Complete | Production-ready single-player loop | [Backlog](backlog.md) Phase 1 |
| **Phase 2** | 🚧 In Progress | LAN multiplayer (Host Mode) + join codes | This PR |
| **Phase 3** | ⏳ Planned | Shared-world authoritative consumption | [PRD](prd-1.md) Section 5.3 |
| **Phase 4** | ⏳ Planned | Battle royale (hole-vs-hole) | [PRD](prd-1.md) Section 5.4 |
| **Phase 5** | ⏳ Planned | Late join/reconnect | [PRD](prd-1.md) Section 5.5 |
| **Phase 6** | ⏳ Planned | Dedicated server deployment | [Arch Overview](arch_overview.md) |
| **Phase 7** | ⏳ Planned | Expand to desktop | [PRD](prd-1.md) Section 6 |
| **Phase 8** | ⏳ Planned | Console platforms (Switch, PS5) | [PRD](prd-1.md) Section 6 |

**Current implementation** (this PR) provides:
- ✅ Unity project with Photon Fusion integration
- ✅ Host Mode networking (Phase 2 foundation)
- ✅ Basic hole mechanics and object consumption
- ✅ Mobile touch controls (Android/iOS)
- ✅ Network synchronization for positions and state

## 🎯 Key Architectural Decisions

From the strategic docs, here are the core decisions guiding development:

### Networking Strategy
- **Start**: Photon Fusion Host Mode (one player = server + client)
- **Migrate**: Dedicated Server Mode when scaling beyond 8-16 players
- **Authority**: Server-authoritative for all game state (anti-cheat)
- **Sync**: Event-based with consumedSet bitset strategy

### Game Design
- **Core Loop**: Move hole, consume objects, grow bigger
- **Battle Royale**: Larger holes can consume smaller holes and inherit mass
- **Matchmaking**: Join-code only (no public matchmaking initially)
- **Sessions**: Private games, family-friendly, minimal setup

### Technical Approach
- **Engine**: Unity with C# (cross-platform, mobile-first)
- **Code Organization**: Modular assembly definitions (Gameplay, Networking, Presentation separated)
- **Object Spawning**: Deterministic with shared seed (bandwidth optimization)
- **Performance**: Mobile-optimized, object pooling, 60 tick/sec target

### Platform Progression
1. **Phase 1**: iOS/Android (same Wi-Fi)
2. **Phase 2**: Home dedicated server (Raspberry Pi experiment)
3. **Phase 3**: Remote play over internet
4. **Phase 4**: Desktop (Windows/Mac/Linux)
5. **Phase 5**: Consoles (Nintendo Switch, PS5) - subject to certification

## 🔧 Technical Deep Dives

For implementation details on current code:

- **Code Patterns**: See [../DEVELOPMENT.md](../DEVELOPMENT.md)
- **Network Architecture**: See [../ARCHITECTURE.md](../ARCHITECTURE.md)
- **Setup Guide**: See [../PHOTON_SETUP.md](../PHOTON_SETUP.md)
- **Troubleshooting**: See [../TROUBLESHOOTING.md](../TROUBLESHOOTING.md)

## 📝 Documentation Maintenance

### When to Update Strategic Docs

- Architecture changes that affect long-term plans
- New phases or feature decisions
- Changes to technology stack or platform targets
- User story revisions or new acceptance criteria

### When to Update Implementation Guides

- Code structure or pattern changes
- New setup steps or configuration requirements
- Bug fixes or workarounds discovered
- Performance optimization techniques

## 🤝 Contributing

When proposing changes:

1. **Strategic changes**: Update relevant docs/ files and PRD
2. **Implementation changes**: Update root-level guides (DEVELOPMENT.md, etc.)
3. **Both**: Consider impact on phased roadmap and backlog

## 📚 Additional Resources

- **Photon Fusion Docs**: https://doc.photonengine.com/fusion
- **Unity Docs**: https://docs.unity3d.com/
- **Project Summary**: [../PROJECT_SUMMARY.md](../PROJECT_SUMMARY.md)
- **Implementation Checklist**: [../CHECKLIST.md](../CHECKLIST.md)

---

**Last Updated**: 2026-02-12  
**Maintained By**: Project team  
**Status**: Active development - Phase 2 in progress
