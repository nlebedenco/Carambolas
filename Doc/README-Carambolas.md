# Carambolas

## Introduction

This project dates back to 2015 when I came to Canada to study Video Game Design and Development at the Toronto Film School. The original motivation was to 
create a compilation of accessory classes that could be re-used in multiple [Unity3d](https://unity.com) projects. After a while, I started to research network
solutions for a prospect multiplayer game and the focus shifted towards designing a reusable network module. Initially, I approached the problem as a simple
matter of integrating UNet or any other suitable 3rd party library I could find at the time. Soon after, I started bumping into all sorts of problems from
broken assumptions to hidden implementation trade-offs. It was not uncommon to find inflated (almost misleading) feature lists out there. Design
incompatibilities or plain broken implementations of what could have otherwise been considered good concepts were not unusual either. In particular, what
bothered me most was that in many solutions certain aspects seemed randomly arbitrary with little to no explanation of why a specific approach was preferred, 
or a certina limit imposed. I would spend hours inspectig a project's source code taking notes to find how and why something was implemented only to realize 
later that it was in direct contradiction to another (supposedly intentional) assumption made by the library's developer somewhere else. 

All this drove me into more work and eventually I decided to build a lightweight network library myself with a reasonable feature list that I could implement 
and verify. No rush, no deadlines. Just a genuine attempt to implement the best technical solution I could devise. 

Meanwhile, I graduated, went back to a full-time job and had to set this project aside. A year ago, after finding some old notes, I restored my archive of 
prototypes and decided to put together a comprehensive build with all the information I gathered so that not only other people could experiment with it but also
understand the way it worked and why.
