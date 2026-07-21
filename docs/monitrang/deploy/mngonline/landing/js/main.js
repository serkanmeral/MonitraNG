(function () {
  'use strict';

  // Year in footer
  var yearEl = document.getElementById('year');
  if (yearEl) {
    yearEl.textContent = String(new Date().getFullYear());
  }

  // Announce bar dismiss
  var announceBar = document.getElementById('announce-bar');
  var announceClose = document.getElementById('announce-close');
  if (announceBar && announceClose) {
    announceClose.addEventListener('click', function () {
      announceBar.classList.add('is-hidden');
      try {
        sessionStorage.setItem('mng-announce-dismissed', '1');
      } catch (_) { /* ignore */ }
    });
    try {
      if (sessionStorage.getItem('mng-announce-dismissed') === '1') {
        announceBar.classList.add('is-hidden');
      }
    } catch (_) { /* ignore */ }
  }

  // Sticky header shadow
  var header = document.getElementById('site-header');
  if (header) {
    var onScroll = function () {
      header.classList.toggle('is-sticky', window.scrollY > 8);
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
  }

  // Mobile nav toggle
  var navToggle = document.getElementById('nav-toggle');
  var siteNav = document.getElementById('site-nav');
  if (navToggle && siteNav) {
    navToggle.addEventListener('click', function () {
      var open = siteNav.classList.toggle('is-open');
      navToggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    });
    siteNav.querySelectorAll('a').forEach(function (link) {
      link.addEventListener('click', function () {
        siteNav.classList.remove('is-open');
        navToggle.setAttribute('aria-expanded', 'false');
      });
    });
  }

  // Feature tabs
  document.querySelectorAll('[data-tabs]').forEach(function (tabsRoot) {
    var tabButtons = tabsRoot.querySelectorAll('.tabs__tab');
    var panels = tabsRoot.querySelectorAll('.tabs__panel');

    tabButtons.forEach(function (btn) {
      btn.addEventListener('click', function () {
        var target = btn.getAttribute('data-tab');
        tabButtons.forEach(function (b) {
          var active = b === btn;
          b.classList.toggle('is-active', active);
          b.setAttribute('aria-selected', active ? 'true' : 'false');
        });
        panels.forEach(function (panel) {
          var show = panel.getAttribute('data-panel') === target;
          panel.classList.toggle('is-active', show);
          if (show) {
            panel.removeAttribute('hidden');
          } else {
            panel.setAttribute('hidden', '');
          }
        });
      });
    });
  });

  // FAQ accordion: single open in FAQ section only
  var faqAccordion = document.querySelector('.accordion--faq');
  if (faqAccordion) {
    faqAccordion.querySelectorAll('.accordion__item').forEach(function (item) {
      item.addEventListener('toggle', function () {
        if (!item.open) return;
        faqAccordion.querySelectorAll('.accordion__item').forEach(function (other) {
          if (other !== item) other.open = false;
        });
      });
    });
  }
})();
